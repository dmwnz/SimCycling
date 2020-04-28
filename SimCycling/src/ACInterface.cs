using System;
using System.Collections.Generic;
using System.Numerics;

using SimCycling.Utils;
using AssettoCorsaSharedMemory;
using SimCycling.State;

namespace SimCycling
{
    class ACInterface
    {
        AssettoCorsa ac;
        PID pid;
        volatile bool updateLocked = false;

        Vector3 frontCoordinates = new Vector3(0, 0, 0);
        Vector3 rearCoordinates = new Vector3(0, 0, 0);
        Vector3 carCoordinates = new Vector3(0, 0, 0);

        string track;
        string layout;

        float trackLength;
        string acLocation;
        bool isSpeedInit;
        bool isInPit;

        List<Updateable> updateables;
        JoyControl joyControl;

        bool useAssistLine = true;
        AssistLineFollower assistLineFollower = new AssistLineFollower();

        public ACInterface(List<Updateable> updateables, JoyControl joyControl, string acLocation)
        {
            this.updateables = updateables;
            this.joyControl = joyControl;
            this.acLocation = acLocation;
            Start();
        }

        public static void Log(String s, params object[] parms)
        {
            Console.WriteLine(s, parms);
        }

        public void Stop()
        {

            ac.PhysicsUpdated -= OnACPhysics;
            ac.GraphicsUpdated -= OnACGraphics;
            ac.StaticInfoUpdated -= OnACInfo;
            ac.Stop();
        }

        private void Start()
        {
            Log("Init Ac");
            ac = new AssettoCorsa
            {
                PhysicsInterval = 100,
                GraphicsInterval = 500,
                StaticInfoInterval = 1000
            };

            ac.PhysicsUpdated += OnACPhysics;
            ac.GraphicsUpdated += OnACGraphics;
            ac.StaticInfoUpdated += OnACInfo;


            ac.Start();
            isSpeedInit = false;
            Log("AC Is running : " + ac.IsRunning);


            var P = 0.3f;
            var I = 0.005f;
            var D = 0.0f;

            pid = new PID(P, I, D);
            pid.Clear();
        }
        private void OnACInfo(object sender, StaticInfoEventArgs e)
        {
            if (e.StaticInfo.Track != track || e.StaticInfo.TrackConfiguration != layout)
            {
                track = e.StaticInfo.Track;
                layout = e.StaticInfo.TrackConfiguration;
                Console.WriteLine("Track = {0}, Config = {1}", track, layout);
                assistLineFollower.Load(e.StaticInfo.Track, e.StaticInfo.TrackConfiguration);
            }

            trackLength = e.StaticInfo.TrackSPlineLength;
            assistLineFollower.TrackLength = trackLength;
        }

        

        private void OnACGraphics(object sender, GraphicsEventArgs e)
        {
            if (updateLocked)
            {
                return;
            }

            updateLocked = true;

            var altitudeDiff = frontCoordinates.Y - rearCoordinates.Y;
            var distance = Consts.Norm(rearCoordinates - frontCoordinates);


            var newPitch = (float)Math.Round(altitudeDiff * 1000.0f / distance) / 10.0f;
            if (float.IsNaN(newPitch))
            {
                newPitch = 0;
            }
            AntManagerState.GetInstance().BikeIncline = newPitch;

            foreach (Updateable updateable in updateables)
            {
                updateable.Update();
            }
            isInPit = (bool)(e.Graphics.IsInPitLane > 0);
            updateLocked = false;
        }
        private void OnACPhysics(object sender, PhysicsEventArgs e)
        {
            //try
            //{
            RaceState.ReadFromMemory();



            if (RaceState.GetInstance().CarVelocities == null || RaceState.GetInstance().CarVelocities.Count == 0)
            {
                return;
            }

            if (RaceState.GetInstance().CarPositions == null || RaceState.GetInstance().CarPositions.Count == 0)
            {
                return;
            }

            var frontX =(float) ((e.Physics.TyreContactPoint[0].X + e.Physics.TyreContactPoint[1].X) / 2.0);
            var frontY = (float) ((e.Physics.TyreContactPoint[0].Y + e.Physics.TyreContactPoint[1].Y) / 2.0);
            var frontZ = (float) ((e.Physics.TyreContactPoint[0].Z + e.Physics.TyreContactPoint[1].Z) / 2.0);

            var rearX = (float)((e.Physics.TyreContactPoint[2].X + e.Physics.TyreContactPoint[3].X) / 2.0);
            var rearY = (float)((e.Physics.TyreContactPoint[2].Y + e.Physics.TyreContactPoint[3].Y) / 2.0);
            var rearZ = (float)((e.Physics.TyreContactPoint[2].Z + e.Physics.TyreContactPoint[3].Z) / 2.0);


            frontCoordinates = new Vector3(frontX, frontY, frontZ);
            rearCoordinates = new Vector3(rearX, rearY, rearZ);
            if (!isSpeedInit)
            {
                Console.WriteLine("Resetting speed.");
                AntManagerState.GetInstance().BikeSpeedKmh = e.Physics.SpeedKmh;
                isSpeedInit = true;
            }

            carCoordinates = RaceState.GetInstance().CarPositions[0];

           
            AntManagerState.GetInstance().AirDensity = e.Physics.AirDensity;

            if (useAssistLine)
            {
                if (RaceState.GetInstance().CarPositions.Count == 0 || RaceState.GetInstance().NormalizedCarPositions.Count == 0)
                {
                    Console.WriteLine("No car positions..");
                    return;
                }
                var orientation = frontCoordinates - rearCoordinates;
                orientation = Vector3.Normalize(orientation);
                assistLineFollower.CarOrientation = orientation;
                assistLineFollower.CarPosition = RaceState.GetInstance().CarPositions[0];
                assistLineFollower.CarVelocity = RaceState.GetInstance().CarVelocities[0];
                assistLineFollower.NormalizedCarPosition = RaceState.GetInstance().NormalizedCarPositions[0];
                assistLineFollower.Update(RaceState.GetInstance());
                if (AntManagerState.GetInstance().BikeSpeedKmh > assistLineFollower.SpeedLimit)
                {
                    AntManagerState.GetInstance().BikeSpeedKmh = assistLineFollower.SpeedLimit;
                }
                joyControl.Direction(10*assistLineFollower.Direction); // Should be ratio between steering value and angle
            }
            else
            {
                joyControl.Direction(0);
            }
            var acSpeed = e.Physics.SpeedKmh;

            var targetSpeed = AntManagerState.GetInstance().BikeSpeedKmh;

            if (targetSpeed > 1)
            {
                var throttle = 10 * (targetSpeed - acSpeed) / (10 + targetSpeed);
                joyControl.Throttle(Math.Max(0, throttle));
            }
        }
    }
}
