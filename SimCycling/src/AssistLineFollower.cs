using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Globalization;
using SimCycling.Utils;
using AcTools.AiFile;
using AcTools.Utils;

namespace SimCycling
{
    class AssistLineFollower
    {

        AiSpline aiSpline;
        AiSpline aiSplinePits;

        Vector3 targetPoint = new Vector3(0, 0, 0);

        float anticipationTime = 1; //look 1s in advance for steering and collision avoidance

        float rangeThreshold = 6; // assist line to target are in this range (in m)
        float passingForwardDistance = 5;
        float passingSideDistance = 2;
        float maxPitDistance = 10;
        float targetSide = 0;

        Dictionary<int, float> currentlyOvertakenCars = new Dictionary<int, float>();

        public float NormalizedCarPosition { get; set; }
        public float TrackLength { get; set; }
        public Vector3 CarOrientation { get; set; }
        public Vector3 CarPosition { get; set; }
        public Vector3 CarVelocity { get; set; }
        public float Direction { get; set; }
        public float SpeedLimit { get; set; }

        public AssistLineFollower()
        {
          
        }

        public bool Load(string track, string layout="")
        {
            string assistLocation;
            string acLocation = Environment.GetEnvironmentVariable("AC_ROOT");
            if (acLocation == null)
            {
                acLocation =  AcRootFinder.TryToFind();
            }
           
            Console.WriteLine("Found AC : {0}", acLocation);
            if (layout == "")
            {
                assistLocation = string.Format(@"{0}\content\tracks\{1}\ai\fast_lane.ai", acLocation, track);
            }
            else
            {
                assistLocation = string.Format(@"{0}\content\tracks\{1}\{2}\ai\fast_lane.ai", acLocation, track, layout);
            }
            aiSpline = AiSpline.FromFile(assistLocation);

            if (layout == "")
            {
                assistLocation = string.Format(@"{0}\content\tracks\{1}\ai\pit_lane.ai", acLocation, track);
            }
            else
            {
                assistLocation = string.Format(@"{0}\content\tracks\{1}\{2}\ai\pit_lane.ai", acLocation, track, layout);
            }
            aiSplinePits = AiSpline.FromFile(assistLocation);

            Console.WriteLine("Loaded AI Line. N points = {0}.", aiSpline.Points.Length);
            return true;
        }

        private int FindClosestPointIndex(AiPoint[] points, Vector3 targetPoint)
        {
            int index = 0;
            float minValue = 10000;
            for (int i = 0; i < points.Length; i++)
            {
                var newValue = Vector3.Distance(Consts.FromArray(points[i].Position), targetPoint);
                if (newValue < minValue)
                {
                    index = i; minValue = newValue;
                }
            }
            return index;
        }

        private bool IsInLane(AiPoint point, AiPointExtra pointExtra, Vector3 targetPoint)
        {
            Vector3 forwardVector = Consts.FromArray(pointExtra.ForwardVector);
            Vector3 sideVector = new Vector3(-forwardVector.Z, 0, forwardVector.X);
            sideVector = Vector3.Normalize(sideVector);
            Vector3 position = Consts.FromArray(point.Position);
            var side = Vector3.Dot(targetPoint - position, sideVector);
            if (side > 0)
            {
                return (side <= pointExtra.SideRight);
            }
            else
            {
                return (-side <= pointExtra.SideLeft);
            }
        }

        private int MoveIndexBy(AiPoint[] points, int index, float dist)
        {
            Vector3 currentPoint = Consts.FromArray(points[index].Position);
            var newIndex = index;
            Vector3 newPoint = Consts.FromArray(points[newIndex].Position);
            bool flag = false;
            while (Vector3.Distance(currentPoint, newPoint) < dist)
            {
                newIndex++;
                if (newIndex == points.Length)
                {
                    if (!flag)
                    {
                        newIndex = 0;
                        flag = true;
                    }
                    else
                    {
                        break;
                    }
                    
                }
                newPoint = Consts.FromArray(points[newIndex].Position);
            }
            return newIndex;
        }

        private List<Tuple<int, float>> CollisionCheck(Vector3 position, Vector3 sideVector, List<SerializableVector3> carPositions, List<SerializableVector3> carVelocities)
        {
            var result = new List<Tuple<int, float>>();
            Vector3 anticipatedPosition = CarPosition + CarVelocity * anticipationTime;

            for (int j = 1; j < carPositions.Count; j++) // j=0 is our car
            {
                var d = Vector3.Distance(CarPosition, carPositions[j]);
                var anticipatedD = Vector3.Distance(anticipatedPosition, carPositions[j]);
                if (Consts.Norm(carVelocities[j]) < Consts.Norm(CarVelocity) && (d < passingForwardDistance || anticipatedD < passingForwardDistance))
                {
                    Console.WriteLine("Passing car {0}", j);
                    result.Add(new Tuple<int, float>(j, Vector3.Dot(carPositions[j] - position, sideVector)));
                }
            }
            return result;
        }

        public void Update(RaceState state)
        {


            var length = NormalizedCarPosition * TrackLength;

            int index = FindClosestPointIndex(aiSpline.Points, CarPosition);
            bool isInFastLane = IsInLane(aiSpline.Points[index], aiSpline.PointsExtra[index], CarPosition);
            int indexPits = FindClosestPointIndex(aiSplinePits.Points, CarPosition);
            bool isInPitLane = IsInLane(aiSplinePits.Points[indexPits], aiSplinePits.PointsExtra[indexPits], CarPosition);
            var pt = Consts.FromArray(aiSpline.Points[index].Position);
            var ptPits = Consts.FromArray(aiSplinePits.Points[indexPits].Position);



            AiPoint[] points; AiPointExtra[] pointsExtra;
            if (isInFastLane || Vector3.Distance(ptPits, CarPosition) > maxPitDistance) {
                Console.WriteLine("In fast lane.");
                points = aiSpline.Points;
                pointsExtra = aiSpline.PointsExtra;
            }
            else
            {
                Console.WriteLine("In pit lane.");
                index = indexPits;
                points = aiSplinePits.Points;
                pointsExtra = aiSplinePits.PointsExtra;
            }

            index = MoveIndexBy(points, index, Consts.Norm(CarVelocity) * anticipationTime);

            var point = points[index];
            var pointExtra = pointsExtra[index];
            Vector3 forwardVector = Consts.FromArray(pointExtra.ForwardVector);
            if (Consts.Norm(forwardVector) == 0)
            {
                var nextIndex = index + 1;
                if (nextIndex == points.Length)
                {
                    nextIndex = 0;
                }
                forwardVector = Consts.FromArray(points[nextIndex].Position) - Consts.FromArray(points[index].Position);
            }
            Vector3 sideVector = new Vector3(-forwardVector.Z, 0, forwardVector.X);
            sideVector = Vector3.Normalize(sideVector);
            Vector3 position = Consts.FromArray(point.Position);

            var collisionCheck = CollisionCheck(position, sideVector, state.CarPositions, state.CarVelocities);

            SpeedLimit = float.MaxValue;
            Console.Write("New car (s) to overtake !");
            for (int i = 0; i < collisionCheck.Count; i++)
            {
                Console.Write(" {0} ", collisionCheck[i].Item1);
                        
            }
            Console.WriteLine();
            collisionCheck.Add(new Tuple<int, float>(-1, -pointExtra.SideLeft));
            collisionCheck.Add(new Tuple<int, float>(-1, pointExtra.SideRight));
            collisionCheck.Sort((x, y) => x.Item2.CompareTo(y.Item2));
                

            var admissibleIntervals = new List<Tuple<float, float>>();
            Console.WriteLine("Obstacle : {0}", collisionCheck[0].Item2);
            for (int i = 1; i < collisionCheck.Count; i++)
            {
                Console.WriteLine("Obstacle : {0}", collisionCheck[i].Item2);
                if (collisionCheck[i].Item2 - collisionCheck[i - 1].Item2 > 2 * passingSideDistance)
                {
                    admissibleIntervals.Add(new Tuple<float, float>(collisionCheck[i - 1].Item2, collisionCheck[i].Item2));
                }
            }

                
            if (admissibleIntervals.Count == 0)
            {
                for (int j = 0;  j < collisionCheck.Count; j++) 
                {
                    var c = collisionCheck[j];
                    if (c.Item1 >= 0)
                    {
                        if (Vector3.Dot(CarPosition - state.CarPositions[c.Item1], CarVelocity) < 0)
                        {
                            SpeedLimit = Math.Min(SpeedLimit, Consts.Norm(state.CarVelocities[c.Item1]));
                        }
                    }
                }
                targetSide = 0;
                    
                Console.WriteLine("Cannot overtake, limiting speed and stopping overtake.");
            }
            else
            {

                admissibleIntervals.Sort((x, y) => Math.Abs(x.Item2 + x.Item1).CompareTo(Math.Abs(y.Item2 + y.Item1)));

                var interval = admissibleIntervals[0];
                var leftVal = interval.Item1;
                var rightVal = interval.Item2;
                Console.WriteLine("Passing interval : l = {0}, r = {1}", interval.Item1, interval.Item2);


                if (leftVal > -passingSideDistance)
                {
                    targetSide = leftVal + passingSideDistance;
                }
                else if (rightVal < passingSideDistance)
                {
                    targetSide = rightVal - passingSideDistance;
                }
                else
                {
                    targetSide = 0;
                }
            }

            Vector3 anticipatedPosition = CarPosition + CarVelocity * anticipationTime;




            Console.WriteLine("forwards = {0}", forwardVector);


          
            var targetPoint = position + targetSide * sideVector;

            var toLine = targetPoint - CarPosition;
            var carOrientationAngle = (float)Math.Atan2(CarOrientation.Z, CarOrientation.X);
            var toLineAngle = (float)Math.Atan2(toLine.Z, toLine.X);


            float angle = -toLineAngle + carOrientationAngle;
            if (angle > Math.PI)
            {
                angle -= 2 * (float)Math.PI;
            }
            if (angle < -Math.PI)
            {
                angle += 2 * (float)Math.PI;
            }
            Console.WriteLine("angle = {0}", 180 / (float)Math.PI * angle);

            if (!float.IsNaN(angle))
            {
                Direction = -angle / (float)Math.PI;
            }
            else
            {
                Direction = 0;
            }

        }

    }
}
