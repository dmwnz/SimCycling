using SimCycling.State;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml.Serialization;

namespace SimCycling.Workout
{
    public class Segment
    {
        public float StartTimeSeconds;
        public float DurationSeconds;
        public float TargetRelativePowerIntensity;
        public int TargetCadence;
        public float EndTimeSeconds => StartTimeSeconds + DurationSeconds;
        public string Type;
        public Segment(float lastEndTimeSeconds, float durationSeconds, float powerIntensity, int cadence, Type type)
        {
            StartTimeSeconds = lastEndTimeSeconds;
            DurationSeconds = durationSeconds;
            TargetRelativePowerIntensity = powerIntensity;
            TargetCadence = cadence;
            Type = type.ToString();
        }
    }

    public abstract class GenericWorkout
    {
        public bool IsFinished
        {
            get
            {
                return CurrentSegment == null;
            }
        }
        protected List<Segment> Segments { get; set; } = new List<Segment>();

        protected Segment CurrentSegment
        {
            get
            {
                return Segments.FirstOrDefault(s => s.StartTimeSeconds <= AntManagerState.GetInstance().WorkoutElapsedTime && s.EndTimeSeconds > AntManagerState.GetInstance().WorkoutElapsedTime);
            } 
        }

        protected Segment NextSegment
        {
            get
            {
                 return Segments.FirstOrDefault(s => s.StartTimeSeconds > AntManagerState.GetInstance().WorkoutElapsedTime);
            }
        }

        public static GenericWorkout Factory(string filename)
        {
            if (filename.EndsWith(".js"))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(JsonWorkout));
                var array = File.ReadAllBytes(filename);
                string json = Encoding.UTF8.GetString(array);
                Console.WriteLine(json);
                return (JsonWorkout)serializer.ReadObject(new MemoryStream(array));
            }
            else if (filename.EndsWith(".zwo"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(ZwiftWorkoutFile));
                var fileStream = File.OpenRead(filename);
                var zwiftFile = (ZwiftWorkoutFile)serializer.Deserialize(fileStream);
                return new ZwiftWorkout(zwiftFile);
            }
            else if (filename.EndsWith(".erg") || filename.EndsWith(".mrc") || filename.EndsWith(".txt"))
            {
                return ErgMrcWorkout.LoadFromFile(filename);
            }
            throw new Exception(String.Format("Unrecognized workout file : {0}", filename));
        }

        public void Update()
        {
            AntManagerState state = AntManagerState.GetInstance();
            var secondsSinceStart = state.WorkoutElapsedTime;

            var currentSegment = CurrentSegment;
            var nextSegment = NextSegment;

            if (currentSegment == null)
            {
                state.TargetPower = 0;
            }
            else
            {
                Console.WriteLine("Target power : {0}", currentSegment.TargetRelativePowerIntensity);
                state.TargetPower = currentSegment.TargetRelativePowerIntensity * state.CriticalPower;
                state.RemainingIntervalTime = currentSegment.EndTimeSeconds - secondsSinceStart;
                state.RemainingTotalTime = Segments.Last().EndTimeSeconds - secondsSinceStart;
                if (nextSegment != null)
                {
                    state.NextTargetPower = nextSegment.TargetRelativePowerIntensity * state.CriticalPower;
                }
            }
        }

    }
}