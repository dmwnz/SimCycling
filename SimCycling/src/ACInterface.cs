using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Configuration;
using System.Windows.Media.Media3D;

using SimCycling.Utils;
using AssettoCorsaSharedMemory;
using System.IO.MemoryMappedFiles;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;



namespace SimCycling
{


    [DataContract]
    public class RaceState
    {
        // Singleton instance
        private static RaceState raceState = new RaceState();
        private static MemoryMappedFile mm;

        [DataMember(Name = "car_positions")]
        public List<Vector3D> CarPositions { get; set; } = new List<Vector3D>(); // Position of all cars in the race, 0 is the player

        [DataMember(Name = "car_velocities")]
        public List<Vector3D> CarVelocities { get; set; } = new List<Vector3D>();


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

        Vector3D frontCoordinates = new Vector3D(0, 0, 0);
        Vector3D rearCoordinates = new Vector3D(0, 0, 0);
        Vector3D carCoordinates = new Vector3D(0, 0, 0);

        Boolean lockTaken = false;

        string track;
        float trackLength;
        string acLocation;
        bool isSpeedInit;
        bool isInPit;

        List<Updateable> updateables;
        JoyControl joyControl;

        bool assistLineFound = false;
        List<AssistLine> assistLines;
        float lateralDistance;
        PID directionPid;
        string assistLocation;
        Vector3D targetPoint = new Vector3D(0, 0, 0);
        Vector3D targetDirection = new Vector3D(0, 0, 0);
        float anticipationTime = 1; //look 1s in advance for steering
        float normalizedCarPosition;
        float rangeThreshold = 5; // assist line to target are in this range (in m)
        float avoidThreshold = 4; // Start doing evasion manoeuvers 


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
                GraphicsInterval = 200,
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
            if (!assistLineFound || e.StaticInfo.Track != track)
            {
                LoadAssistLines(e.StaticInfo.Track);
            }
            track = e.StaticInfo.Track;
            trackLength = e.StaticInfo.TrackSPlineLength;

        }

        private void LoadAssistLines(string track)
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
                var P = 0.2f;
                var I = 0.0f;
                var D = 0.2f;

                directionPid = new PID(P, I, D);
                directionPid.Clear();
                directionPid.SetPoint = 0;
                Log("Found {0} assist lines", assistLines.Count);
                assistLineFound = true;
            }
            else
            {
                assistLineFound = false;
            }
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


            if (assistLineFound)
            {
                normalizedCarPosition = e.Graphics.NormalizedCarPosition;
                /*
                float vel;

                */
            }
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

            var frontX = (e.Physics.TyreContactPoint[0].X + e.Physics.TyreContactPoint[1].X) / 2.0;
            var frontY = (e.Physics.TyreContactPoint[0].Y + e.Physics.TyreContactPoint[1].Y) / 2.0;
            var frontZ = (e.Physics.TyreContactPoint[0].Z + e.Physics.TyreContactPoint[1].Z) / 2.0;

            var rearX = (e.Physics.TyreContactPoint[2].X + e.Physics.TyreContactPoint[3].X) / 2.0;
            var rearY = (e.Physics.TyreContactPoint[2].Y + e.Physics.TyreContactPoint[3].Y) / 2.0;
            var rearZ = (e.Physics.TyreContactPoint[2].Z + e.Physics.TyreContactPoint[3].Z) / 2.0;


            frontCoordinates = new Vector3D(frontX, frontY, frontZ);
            rearCoordinates = new Vector3D(rearX, rearY, rearZ);
            if (!isSpeedInit)
            {
                AntManagerState.GetInstance().BikeSpeedKmh = e.Physics.SpeedKmh;
                isSpeedInit = true;
            }

            carCoordinates = RaceState.GetInstance().CarPositions[0];
            pid.SetPoint = AntManagerState.GetInstance().BikeSpeedKmh;

            var acSpeed = e.Physics.SpeedKmh;
            AntManagerState.GetInstance().AirDensity = e.Physics.AirDensity;
            Log("air density = {0}", AntManagerState.GetInstance().AirDensity);
            pid.Update(acSpeed);
            var coeff = pid.Output;
            joyControl.Throttle(coeff);
            if (assistLineFound)
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
                
                Tuple<Vector3D, Vector3D> pointAndDir;
                var targetPoints = new List<Vector3D>();
                var targetDirections = new List<Vector3D>();
                Vector3D opponentPosition;
                Vector3D opponentVelocity;
                for (int i = 0; i < assistLines.Count; i++)
                {
                    pointAndDir = assistLines[i].GetPointAndDirection(normalizedCarPosition);
                    targetPoints.Add(pointAndDir.Item1);
                    targetDirections.Add(pointAndDir.Item2);
                }
                for (int i = 0; i < assistLines.Count; i++)
                {
                    var remove = false;
                    for (int j = 1; j < state.CarVelocities.Count; j++) // remove assist line if they provoke collision. Implementation could be better..
                    {
                        opponentPosition = state.CarPositions[j];
                        opponentVelocity = state.CarVelocities[j];
                        if (Consts.Norm(opponentVelocity) <= Consts.Norm(state.CarVelocities[0])) // Avoid if going faster
                        {
                            var dist = Consts.Norm(opponentPosition + opponentVelocity * anticipationTime -
                                    (targetPoints[i] + anticipationTime * targetDirections[i] * Consts.Norm(state.CarVelocities[0])));
                            Log("projected distance ={0}", dist);
                            if (dist < avoidThreshold)
                            {
                                remove = true;
                            }
                        }
                    }
                    if (!remove)
                    {
                        isValid.Add(i);
                    }
                }
                Log("n valid = {0}", isValid.Count);
               
                foreach (var i in isValid)
                {
                    var assistLine = assistLines[i];
                    float range = (float) Consts.Norm(targetPoints[i] - state.CarPositions[0]);
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
                Log("in range = {0}", isInRange.Count);
                if (isInRange.Count == 0) // if no assist lines are in range, take the closest
                {
                    selectedIdx = argMin;
                }
                if (isValid.Count == 0)
                {
                    selectedIdx = 0;
                }

                float vel = (float) Consts.Norm(state.CarVelocities[0]);
                var p = normalizedCarPosition + anticipationTime * vel / trackLength;
                if (p > 1)
                {
                    p = p - 1;
                }
                Log("selected = {0}", selectedIdx);
                targetPoint = assistLines[selectedIdx].GetPointAndDirection(p).Item1;
                targetDirection = targetDirections[selectedIdx];

                var direction = (frontCoordinates - rearCoordinates);
                direction.Normalize();
                var vertical = new Vector3D(0, 1, 0);
                vertical = vertical - Vector3D.DotProduct(vertical, direction) * direction;
                var side = Vector3D.CrossProduct(vertical, direction);
                var toLine = targetPoint - frontCoordinates;
                lateralDistance = (float)Vector3D.DotProduct(side, toLine);

                if (!float.IsNaN(lateralDistance))
                {
                    directionPid.Update(lateralDistance);
                    var dir = directionPid.Output;
                    joyControl.Direction(dir);
                }
                else
                {
                    joyControl.Direction(0);
                }
            }
            else
            {
                joyControl.Direction(0);
            }
            // }
            // catch (Exception ex)
            //{
            //  Console.WriteLine(ex.Message);
            //}
        }
    }
}
