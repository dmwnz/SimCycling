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

        float anticipationTime = 1; //look 1s in advance for steering and collision avoidance

        float maxPitDistance = 10;
        float targetSide = 0;
        float minDistance = 1.0f;
        float sideRepulsionIntensity = 1.0f;
        float carRepulsionIntensity = 5.0f;
        float attractionIntensity = 0.3f;
        float targetVelocity = 0.5f;

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

        public void Update(SimCycling.State.RaceState state)
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
                // Console.WriteLine("In fast lane.");
                points = aiSpline.Points;
                pointsExtra = aiSpline.PointsExtra;
            }
            else
            {
                // Console.WriteLine("In pit lane.");
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
            // Console.WriteLine("forwards = {0}", forwardVector);


            SpeedLimit = float.MaxValue;
            float force = 0.0f;

            var leftDistance = targetSide + pointExtra.SideLeft;
            leftDistance = Math.Max(leftDistance, minDistance);
            // Console.WriteLine("left force = {0}", (sideRepulsionIntensity / leftDistance / leftDistance));
            force += sideRepulsionIntensity / leftDistance / leftDistance;

            var rightDistance = targetSide - pointExtra.SideRight;
            rightDistance = Math.Min(rightDistance, -minDistance);
            force += sideRepulsionIntensity / rightDistance / Math.Abs(rightDistance);
            // Console.WriteLine("right force = {0}", sideRepulsionIntensity / rightDistance / Math.Abs(rightDistance));


            for (int i = 1; i < state.CarPositions.Count; i++)
            {
               
                var v = state.CarPositions[i] + anticipationTime * (Vector3) (state.CarVelocities[i]) - position - targetSide * sideVector;
                var d_cut = Math.Max(Consts.Norm(v), minDistance);
                force += - carRepulsionIntensity / d_cut / d_cut * Math.Sign(Vector3.Dot(v, sideVector));
                // Console.WriteLine("car {0} force = {1}", i, -carRepulsionIntensity / d_cut / d_cut * Vector3.Dot(v, sideVector));
            }

            var targetSide_th = Math.Max(Math.Abs(targetSide), minDistance);
            force += (- attractionIntensity / targetSide_th / targetSide_th * targetSide);
            // Console.WriteLine("force = {0}", force);
            targetSide += force * targetVelocity;

            if (targetSide < -pointExtra.SideLeft)
            {
                targetSide = -pointExtra.SideLeft;
            }

            if (targetSide > pointExtra.SideRight)
            {
                targetSide = pointExtra.SideRight;
            }


            var targetPoint = position + targetSide * sideVector;
            // Console.WriteLine("target = {0}", targetSide);

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
            // Console.WriteLine("angle = {0}", 180 / (float)Math.PI * angle);

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
