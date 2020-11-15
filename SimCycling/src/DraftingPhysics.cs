using System;
using System.Numerics;
using System.Resources;
using Dynastream.Fit;
using SimCycling.State;
using SimCycling.Utils;

namespace SimCycling
{

    class DraftingPhysics
    {
        readonly double draftingDuration = 2;
        readonly double maxReduction = 0.5;
        readonly double maxSideDistance = 2;
        readonly float carLength = 2.0f;

        public static void Log(String s, params object[] parms)
        {
            Console.WriteLine(s, parms);
        }
        public DraftingPhysics()
        {

        }

        private double SingleRiderDraftingReduction(Vector3 carVector, Vector3 sideVector, Vector3 relativePosition, Vector3 carVelocity)
        {
            if (Vector3.Dot(carVector, relativePosition) < 0)
            {
                return 0;
            }
            double deltaT = Consts.Norm(relativePosition) / Consts.Norm(carVelocity);
            if (deltaT > draftingDuration)
            {
                return 0;
            }
            double sideDistance = Math.Abs(Vector3.Dot(sideVector, relativePosition));
            if (sideDistance > maxSideDistance)
            {
                return 0;
            }
            return (maxSideDistance - sideDistance) / maxSideDistance * (draftingDuration - deltaT) / draftingDuration * maxReduction;
        }

        public double DraftingCoefficient(SimCycling.State.RaceState state)
        {
            double result = 1.0;
            Vector3 riderCarVector = state.CarVelocities[0];
            riderCarVector = Vector3.Normalize(riderCarVector);
            Vector3 riderFrontPosition = state.CarPositions[0] + Vector3.Multiply(0.5f * carLength, riderCarVector);
            for (int i = 1; i < state.CarPositions.Count; i++)
            {
                Vector3 carVector = state.CarVelocities[i];
                carVector = Vector3.Normalize(carVector);
                Vector3 sideVector = new Vector3(-carVector.Z, 0, carVector.X);
                Vector3 relativePosition = state.CarPositions[i] - Vector3.Multiply(0.5f * carLength, carVector) - riderFrontPosition;
                result = result * (1 - SingleRiderDraftingReduction(carVector, sideVector, relativePosition, state.CarVelocities[i]));
            }
            if (result > 1 - maxReduction)
            {
                result = 1 - maxReduction;
            }
            return result;
        }

    }
}
