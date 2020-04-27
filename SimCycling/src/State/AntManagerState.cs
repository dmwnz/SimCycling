
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;


namespace SimCycling.State
{
    [DataContract]
    class AntManagerState
    {
        // Singleton instance
        private static readonly AntManagerState antManagerState = new AntManagerState();
        private static MemoryMappedFile mm;
        private static volatile bool WriteInProgress = false;
        private const int MEMORY_MAP_SIZE = 1024;

        [DataMember()]
        public int BikeCadence { get; set; }

        [DataMember()]
        public float BikeSpeedKmh { get; set; }

        [DataMember()]
        public float BikeIncline { get; set; }

        [DataMember()]
        public int CyclistHeartRate { get; set; }

        [DataMember()]
        public float CyclistPower { get; set; }

        [DataMember()]
        public float TripTotalKm { get; set; }

        [DataMember()]
        public float TripTotalTime { get; set; }

        [DataMember()]
        public float AirDensity { get; set; }

        [DataMember()]
        public float TargetPower { get; set; }

        [DataMember()]
        public float NextTargetPower { get; set; }

        [DataMember()]
        public float RemainingIntervalTime { get; set; }

        [DataMember()]
        public float RemainingTotalTime { get; set; }

        [DataMember()]
        public float CriticalPower { get; set; }

        [DataMember()]
        public string WorkoutName { get; set; }

        [DataMember()]
        public float WorkoutElapsedTime { get; set; }



        public static AntManagerState GetInstance()
        {
            return antManagerState;
        }

        public static void WriteToMemory()
        {
            if (WriteInProgress)
            {
                return;
            }

            WriteInProgress = true;

            mm = MemoryMappedFile.CreateOrOpen("SimCycling", MEMORY_MAP_SIZE);

            using (var mmAccessor = mm.CreateViewAccessor())
            {
                // Write 0xAA flag to signal readers to wait
                mmAccessor.Write(0, (byte)0xAA);

                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AntManagerState));

                // Initialize MemoryStream that will store Json as binary
                MemoryStream ms = new MemoryStream(MEMORY_MAP_SIZE - 1);
                serializer.WriteObject(ms, GetInstance());

                // Write to shared memory
                mmAccessor.WriteArray(1, ms.GetBuffer(), 0, MEMORY_MAP_SIZE - 1);

                // Write 0X11 flag to signal readers they can read
                mmAccessor.Write(0, (byte)0x11);
            }

            WriteInProgress = false;

        }
    }
}