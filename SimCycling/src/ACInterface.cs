using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Configuration;
using System.Windows.Media.Media3D;
using System.Numerics;

using SimCycling.Utils;
using AssettoCorsaSharedMemory;
using System.IO.MemoryMappedFiles;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;



namespace SimCycling
{
    [DataContract]
    public struct SerializableVector3
    {
        [DataMember]
        public float X;
        [DataMember]
        public float Y;
        [DataMember]
        public float Z;

        public SerializableVector3(float rx, float ry, float rz)
        {
            X = rx;
            Y = ry;
            Z = rz;
        }

        public static implicit operator Vector3(SerializableVector3 rValue)
        {
            return new Vector3(rValue.X, rValue.Y, rValue.Z);
        }

        public static implicit operator SerializableVector3(Vector3 rValue)
        {
            return new SerializableVector3(rValue.X, rValue.Y, rValue.Z);
        }
    }

    [DataContract]
    public class RaceState
    {
        // Singleton instance
        private static RaceState raceState = new RaceState();
        private static MemoryMappedFile mm;

        [DataMember(Name = "car_positions")]
        public List<SerializableVector3> CarPositions { get; set; } = new List<SerializableVector3>(); // Position of all cars in the race, 0 is the player

        [DataMember(Name = "car_velocities")]
        public List<SerializableVector3> CarVelocities { get; set; } = new List<SerializableVector3>();

        [DataMember(Name = "normalized_car_positions")]
        public List<float> NormalizedCarPositions { get; set; } = new List<float>();


        public static RaceState GetInstance()
        {
            return raceState;
        }


        public static byte[] TrimEnd(byte[] array)
        {
            int lastIndex = Array.FindLastIndex(array, b => b != 0);

            Array.Resize(ref array, lastIndex + 1);

            return array;
        }

        public static void ReadFromMemory()
        {
            mm = MemoryMappedFile.CreateOrOpen("SimCyclingRaceState", 1024);

            using (var mmAccessor = mm.CreateViewAccessor())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(RaceState));
                byte[] array = Enumerable.Repeat((byte)0x0, 1024).ToArray();
                mmAccessor.ReadArray<byte>(0, array, 0, 1024);
                array = TrimEnd(array);
                string json = Encoding.UTF8.GetString(array);
                RaceState newState;
                if (array.Length > 0)
                {
                    try
                    {
                        newState = (RaceState)serializer.ReadObject(new MemoryStream(array));
                        raceState = newState;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }
        }
    }

    class ACInterface
    {
        AssettoCorsa ac;
        PID pid;

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

        bool assistLineFound;
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
                PhysicsInterval = 200,
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
        }
        private void OnACPhysics(object sender, PhysicsEventArgs e)
        {
            //try
            //{
            RaceState.ReadFromMemory();


            if (RaceState.GetInstance().CarVelocities.Count == 0)
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
                AntManagerState.GetInstance().BikeSpeedKmh = e.Physics.SpeedKmh;
                isSpeedInit = true;
            }

            carCoordinates = RaceState.GetInstance().CarPositions[0];
            pid.SetPoint = AntManagerState.GetInstance().BikeSpeedKmh;

            var acSpeed = e.Physics.SpeedKmh;
            AntManagerState.GetInstance().AirDensity = e.Physics.AirDensity;
            pid.Update(acSpeed);
            var coeff = pid.Output;
            joyControl.Throttle(coeff);
            if (assistLineFound)
            {
                if (RaceState.GetInstance().CarPositions.Count == 0 || RaceState.GetInstance().NormalizedCarPositions.Count == 0)
                {
                    return;
                }
                var orientation = frontCoordinates - rearCoordinates;
                orientation = Vector3.Normalize(orientation);
                assistLineFollower.CarOrientation = orientation;
                assistLineFollower.CarPosition = RaceState.GetInstance().CarPositions[0];
                Console.WriteLine("car pos = {0},{1},{2}", assistLineFollower.CarPosition.X, assistLineFollower.CarPosition.Y, assistLineFollower.CarPosition.Z);
                Console.WriteLine("car pos = {0},{1},{2}", frontCoordinates.X, frontCoordinates.Y, frontCoordinates.Z);
                assistLineFollower.NormalizedCarPosition = RaceState.GetInstance().NormalizedCarPositions[0];
                assistLineFollower.Update();
                joyControl.Direction(10*assistLineFollower.Direction); // Should be ratio between steering value and angle
            }
            else
            {
                joyControl.Direction(0);
            }
        }
    }
}
