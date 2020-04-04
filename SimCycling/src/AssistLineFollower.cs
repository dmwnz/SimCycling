using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Globalization;
using SimCycling.Utils;

namespace SimCycling
{
    class AssistLineFollower
    {

        List<AssistLine> assistLines;
        string assistLocation;
        Vector3 targetPoint = new Vector3(0, 0, 0);

        float anticipationTime = 1; //look 1s in advance for steering and collision avoidance

        float rangeThreshold = 6; // assist line to target are in this range (in m)
        float avoidThreshold = 6; // Start doing evasion manoeuvers

        public float NormalizedCarPosition { get; set; }
        public float TrackLength { get; set; }
        public Vector3 CarOrientation { get; set; }
        public Vector3 CarPosition { get; set; }
        public float Direction { get; set; }

        public AssistLineFollower()
        {
          
        }

        public bool Load(string track)
        {
            assistLines = new List<AssistLine>();
            int i = 0;
            while (true)
            {
                assistLocation = string.Format(@"data\{0}_{1}.csv", track, i);
                if (!File.Exists(assistLocation))
                {
                    break;
                }
                assistLines.Add(new AssistLine(assistLocation));
                i = i + 1;
            }
            if (assistLines.Count > 0)
            {
                Console.WriteLine("Found {0} assist lines", assistLines.Count);
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Update()
        {
            // Get target assist line
            var isValid = new List<int>();
            var isInRange = new List<int>();
            int argMin = -1;
            float minRange = 100000;
            RaceState state;
            try
            {
                state = RaceState.GetInstance();
            }
            catch
            {
                Console.WriteLine("Could not get race state.");
                return;
            }
            if (state.CarPositions.Count == 0)
            {
                Console.WriteLine("Could not get cat position.");
                return;
            }
            Tuple<Vector3, Vector3> pointAndDir;
            var targetPoints = new List<Vector3>();
            var targetDirections = new List<Vector3>();
            Vector3 opponentPosition;
            float opponentVelocity;
            float carVelocity = Consts.Norm(state.CarVelocities[0]);




            for (int i = 0; i < assistLines.Count; i++)
            {
                pointAndDir = assistLines[i].GetPointAndDirection(NormalizedCarPosition);
                targetPoints.Add(pointAndDir.Item1);
                targetDirections.Add(pointAndDir.Item2);
            }
            for (int i = 0; i < assistLines.Count; i++)
            {
                var remove = false;
                for (int j = 1; j < state.CarVelocities.Count; j++) // remove assist line if they provoke collision. Implementation could be better..
                {
                    opponentPosition = state.CarPositions[j];
                    opponentVelocity = Consts.Norm(state.CarVelocities[j]);

                    var toTarget = targetPoints[i] - CarPosition;
                    var toTargetAngle = (float)Math.Atan2(toTarget.Z, toTarget.X);
                    var toOpponent = opponentPosition - CarPosition;
                    var toOpponentAngle = (float)Math.Atan2(toOpponent.Z, toOpponent.X);
                    var angleDiff = toOpponentAngle - toTargetAngle;
                    if (angleDiff > Math.PI)
                    {
                        angleDiff -= 2 * (float)Math.PI;
                    }
                    if (angleDiff < -Math.PI)
                    {
                        angleDiff += 2 * (float)Math.PI;
                    }
                    var dist = Consts.Norm(toOpponent);
                    var c = (float)Math.Cos(angleDiff);
                    Console.WriteLine("projected distance = {0}", dist);
                    Console.WriteLine("cos = {0}", c);

                    if (carVelocity - opponentVelocity > 0 && dist < avoidThreshold * c)
                    {
                        remove = true;
                    }
                }
                if (!remove)
                {
                    isValid.Add(i);
                }
            }
            Console.WriteLine("n valid = {0}", isValid.Count);

            foreach (var i in isValid)
            {
                var assistLine = assistLines[i];
                float range = (float)Consts.Norm(targetPoints[i] - state.CarPositions[0]);
                if (range < minRange)
                {
                    argMin = i;
                    minRange = range;
                }
                if (range < rangeThreshold)
                {
                    isInRange.Add(i);
                }
            }
            int selectedIdx = 1000;
            foreach (var i in isInRange)
            {
                if (i < selectedIdx)
                {
                    selectedIdx = i;
                }
            }
            Console.WriteLine("in range = {0}", isInRange.Count);
            if (isInRange.Count == 0) // if no assist lines are in range, take the closest
            {
                selectedIdx = argMin;
            }
            if (isValid.Count == 0)
            {
                selectedIdx = 0;
            }

            float vel = (float)Consts.Norm(state.CarVelocities[0]);
            var p = NormalizedCarPosition + anticipationTime * vel / TrackLength;
            if (p > 1)
            {
                p = p - 1;
            }
            Console.WriteLine("selected = {0}", selectedIdx);
            targetPoint = assistLines[selectedIdx].GetPoint(p);

            var toLine = targetPoint - CarPosition;
            Console.WriteLine("distance = {0}", Consts.Norm(toLine));
            var carOrientationAngle = (float)Math.Atan2(CarOrientation.Z, CarOrientation.X);
            var toLineAngle = (float)Math.Atan2(toLine.Z, toLine.X);


            float angle = - toLineAngle + carOrientationAngle;
            if (angle > Math.PI)
            {
                angle -= 2 * (float) Math.PI;
            }
            if (angle < - Math.PI)
            {
                angle += 2 * (float)Math.PI;
            }
            Console.WriteLine("angle = {0}", 180/(float) Math.PI * angle);

            if (!float.IsNaN(angle))
            {
                Direction = - angle / (float)Math.PI;
            }
            else
            {
                Direction = 0;
            }
        }

    }
}
