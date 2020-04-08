
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



        public static AntManagerState GetInstance()
        {
            return antManagerState;
        }

        public static void WriteToMemory()
        {
            mm = MemoryMappedFile.CreateOrOpen("SimCycling", 1024);

            using (var mmAccessor = mm.CreateViewAccessor())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AntManagerState));
                byte[] array = Enumerable.Repeat((byte)0x0, 1024).ToArray();
                MemoryStream ms = new MemoryStream(array);
                serializer.WriteObject(ms, GetInstance());
                mmAccessor.WriteArray(0, array, 0, 1024);
            }

        }
    }
}