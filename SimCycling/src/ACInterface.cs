using System;
using System.Collections.Generic;
using System.Numerics;

using SimCycling.Utils;
using AssettoCorsaSharedMemory;
using SimCycling.State;

namespace SimCycling
{
    public delegate void NewLapHandler();

    class ACInterface
    {
        public event NewLapHandler NewLap;

        AssettoCorsa ac;
        volatile bool updateLocked = false;

        Vector3 frontCoordinates = new Vector3(0, 0, 0);
        Vector3 rearCoordinates = new Vector3(0, 0, 0);

        string track;
        string layout;

        float trackLength;
        bool isSpeedInit;

        List<Updateable> updateables;
        JoyControl joyControl;

        bool useAssistLine = true;
        AssistLineFollower assistLineFollower = new AssistLineFollower();
        private float previousNormalizedCarPosition;

        public ACInterface(List<Updateable> updateables, JoyControl joyControl)
        {
            this.updateables = updateables;
            this.joyControl = joyControl;
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

            if (RaceState.Instance.NormalizedCarPositions.Count > 0)
            {
                // Hack to detect new lap
                // NormalizedCarPosition ranges from [0 ; 1]
                // If it goes from >0.9 to <0.1 then surely it's a new lap
                if (RaceState.Instance.NormalizedCarPositions[0] < previousNormalizedCarPosition - 0.975)
                {
                    Console.WriteLine("New Lap !");
                    NewLap();
                }
                previousNormalizedCarPosition = RaceState.Instance.NormalizedCarPositions[0];
            }

            var altitudeDiff = frontCoordinates.Y - rearCoordinates.Y;
            var distance = Consts.Norm(rearCoordinates - frontCoordinates);

            var newPitch = (float)Math.Round(altitudeDiff * 1000.0f / distance) / 10.0f;
            if (float.IsNaN(newPitch))
            {
                newPitch = 0;
            }
            AntManagerState.Instance.BikeIncline = newPitch;

            foreach (Updateable updateable in updateables)
            {
                updateable.Update();
            }
            updateLocked = false;
        }
        private void OnACPhysics(object sender, PhysicsEventArgs e)
        {
            RaceState.ReadFromMemory();

            if (RaceState.Instance.CarVelocities == null || RaceState.Instance.CarVelocities.Count == 0)
            {
                return;
            }

            if (RaceState.Instance.CarPositions == null || RaceState.Instance.CarPositions.Count == 0)
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
                AntManagerState.Instance.BikeSpeedKmh = e.Physics.SpeedKmh;
                isSpeedInit = true;
            }
           
            AntManagerState.Instance.AirDensity = e.Physics.AirDensity;

            if (useAssistLine)
            {
                if (RaceState.Instance.CarPositions.Count == 0 || RaceState.Instance.NormalizedCarPositions.Count == 0)
                {
                    Console.WriteLine("No car positions..");
                    return;
                }
                var orientation = frontCoordinates - rearCoordinates;
                orientation = Vector3.Normalize(orientation);
                assistLineFollower.CarOrientation = orientation;
                assistLineFollower.CarPosition = RaceState.Instance.CarPositions[0];
                assistLineFollower.CarVelocity = RaceState.Instance.CarVelocities[0];
                assistLineFollower.NormalizedCarPosition = RaceState.Instance.NormalizedCarPositions[0];
                assistLineFollower.Update(RaceState.Instance);
                if (AntManagerState.Instance.BikeSpeedKmh > assistLineFollower.SpeedLimit)
                {
                    AntManagerState.Instance.BikeSpeedKmh = assistLineFollower.SpeedLimit;
                }
                joyControl.Direction(10*assistLineFollower.Direction); // Should be ratio between steering value and angle
            }
            else
            {
                joyControl.Direction(0);
            }
            var acSpeed = e.Physics.SpeedKmh;

            var targetSpeed = AntManagerState.Instance.BikeSpeedKmh;

            if (targetSpeed > 1)
            {
                var throttle = 10 * (targetSpeed - acSpeed) / (10 + targetSpeed);
                joyControl.Throttle(Math.Max(0, throttle));
            }
        }
    }
}
