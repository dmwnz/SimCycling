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
        double CdA, Cxx, riderMass, drivetrainEfficiency;
        DateTime previousFrameTimestamp;
        public static void Log(String s, params object[] parms)
        {
            Console.WriteLine(s, parms);
        }
        public BikePhysics(double CdA, double Cxx, double riderMass, double drivetrainEfficiency)
        {
            this.CdA = CdA;
            this.Cxx = Cxx;
            this.riderMass = riderMass;
            this.drivetrainEfficiency = drivetrainEfficiency;
            previousFrameTimestamp = DateTime.Now;
        }

        override public void Update()
        {
            var time = DateTime.Now;
            double dt = time.Subtract(previousFrameTimestamp).TotalSeconds;
            previousFrameTimestamp = time;
            var dv = DeltaV(dt);
            AntManagerState.Instance.BikeSpeedKmh += (float) (dv * 3.6);
            if (AntManagerState.Instance.BikeSpeedKmh < 0)
            {
                AntManagerState.Instance.BikeSpeedKmh = 0;
            }
        }

        private double GravityAcceleration =>  AntManagerState.Instance.BikeIncline / 100.0 * gravitationAcceleration;

        private double BikeSpeed => AntManagerState.Instance.BikeSpeedKmh / 3.6;

        private double Resistance => 0.5 * AntManagerState.Instance.AirDensity *CdA * BikeSpeed * BikeSpeed + riderMass * Cxx * gravitationAcceleration;

        private double DeltaV(double dt)
        {
            double acceleration = Resistance / riderMass + GravityAcceleration;
            double a = 1; 
            double b = BikeSpeed - acceleration * dt;
            double p = AntManagerState.Instance.CyclistPower * drivetrainEfficiency;
            if (p == 0) { p = -10; } // brake when power is 0;
            double c = - p * dt / riderMass + acceleration * BikeSpeed * dt;
            double delta = b * b - 4 * a * c;
            if (delta < 0)
            {
                return -BikeSpeed;
            }
            return (-b + Math.Sqrt(delta)) / 2 / a;
        }

    }
}
