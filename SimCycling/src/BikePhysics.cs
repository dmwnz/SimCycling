using System;
using SimCycling.State;

namespace SimCycling
{
    abstract class Updateable
    {
        abstract public void Update();
    }
    class BikePhysics : Updateable
    {
        double gravitationAcceleration = 9.81;
        double CdA, Cxx, airDensity, riderMass;
        DateTime previousFrameTimestamp;
        public static void Log(String s, params object[] parms)
        {
            Console.WriteLine(s, parms);
        }
        public BikePhysics(double CdA, double Cxx, double airDensity, double riderMass)
        {
            this.CdA = CdA;
            this.Cxx = Cxx;
            this.airDensity = airDensity;
            this.riderMass = riderMass;
            previousFrameTimestamp = DateTime.Now;
        }

        override public void Update()
        {
            var time = DateTime.Now;
            double dt = time.Subtract(previousFrameTimestamp).TotalSeconds;
            previousFrameTimestamp = time;
            var dv = DeltaV(dt);
            AntManagerState.GetInstance().BikeSpeedKmh += (float) (dv * 3.6);
            if (AntManagerState.GetInstance().BikeSpeedKmh < 0)
            {
                AntManagerState.GetInstance().BikeSpeedKmh = 0;
            }
        }

        private double GravityAcceleration()
        {
            return AntManagerState.GetInstance().BikeIncline / 100.0 * gravitationAcceleration;
        }

        private double BikeSpeed() => AntManagerState.GetInstance().BikeSpeedKmh / 3.6;

        private double Resistance() => 0.5 * AntManagerState.GetInstance().AirDensity *CdA * BikeSpeed() * BikeSpeed() + riderMass * Cxx * gravitationAcceleration;

        private double DeltaV(double dt)
        {
            double acceleration = Resistance() / riderMass + GravityAcceleration();
            double a = 1; double b = BikeSpeed() - acceleration * dt;
            double c = - AntManagerState.GetInstance().CyclistPower * dt / riderMass + acceleration * BikeSpeed() * dt;
            double delta = b * b - 4 * a * c;
            if (delta < 0)
            {
                return -BikeSpeed();
            }
            return (-b + Math.Sqrt(delta)) / 2 / a;
        }

    }
}
