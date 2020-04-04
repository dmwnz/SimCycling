using System;
using System.Text;
using SimCycling.Utils;
using AssettoCorsaSharedMemory;
using vJoyInterfaceWrap;
using System.IO;
using AntPlus.Profiles.Common;
using System.Globalization;
using AntPlus.Profiles.FitnessEquipment;
using System.Configuration;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace SimCycling
{
    [DataContract]
    public class Power
    {
        [DataMember(Name = "max_intensity")]
        public float Intensity { get; set; }
    }

    [DataContract]
    public class WorkoutSegment
    {
        [DataMember(Name = "cadence")]
        public int? Cadence { get; set; }

        [DataMember(Name = "duration_ms")]
        public int DurationMs { get; set; }

        [DataMember(Name = "power")]
        public Power Power { get; set; }

        [DataMember(Name = "segment_type")]
        public string Type { get; set; }

        [DataMember(Name = "start_time")]
        public int StartTime { get; set; }
    }

        [DataContract]
    public class Workout
    {
        [DataMember(Name = "segments")]
        public List<WorkoutSegment> Segments { get; set; }

        public static Workout Factory(string filename)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Workout));
            var array = File.ReadAllBytes(filename);
            string json = Encoding.UTF8.GetString(array);
            Console.WriteLine(json);
            return (Workout) serializer.ReadObject(new MemoryStream(array));
        }
    }
}
