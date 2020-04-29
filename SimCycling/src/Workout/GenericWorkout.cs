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

        public List<SegmentMessage> Messages;

        public Segment(float startTimeSeconds, float durationSeconds, float powerIntensity, int cadence, Type type, List<SegmentMessage> messages = null)
        {
            StartTimeSeconds = startTimeSeconds;
            DurationSeconds = durationSeconds;
            TargetRelativePowerIntensity = powerIntensity;
            TargetCadence = cadence;
            Type = type.ToString();
            Messages = messages;
        }
    }

    public class SegmentMessage
    {
        public string MessageText;

        public float StartDisplayTime { get; internal set; }
        public float EndDisplayTime { get; internal set; }
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
                return Segments.FirstOrDefault(s => s.StartTimeSeconds <= AntManagerState.Instance.WorkoutElapsedTime && s.EndTimeSeconds > AntManagerState.Instance.WorkoutElapsedTime);
            } 
        }

        protected Segment NextSegment
        {
            get
            {
                 return Segments.FirstOrDefault(s => s.StartTimeSeconds > AntManagerState.Instance.WorkoutElapsedTime);
            }
        }

        protected String CurrentMessage
        {
            get
            {
                return CurrentSegment
                    ?.Messages
                    ?.FirstOrDefault(
                        m => AntManagerState.Instance.WorkoutElapsedTime >= m.StartDisplayTime 
                          && AntManagerState.Instance.WorkoutElapsedTime <  m.EndDisplayTime)
                    ?.MessageText;
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
            var secondsSinceStart = AntManagerState.Instance.WorkoutElapsedTime;

            var currentSegment = CurrentSegment;
            var nextSegment = NextSegment;

            if (currentSegment == null)
            {
                AntManagerState.Instance.TargetPower = 0;
            }
            else
            {
                Console.WriteLine("Target power : {0}", currentSegment.TargetRelativePowerIntensity);
                AntManagerState.Instance.TargetPower = currentSegment.TargetRelativePowerIntensity * AntManagerState.Instance.CriticalPower;
                AntManagerState.Instance.RemainingIntervalTime = currentSegment.EndTimeSeconds - secondsSinceStart;
                AntManagerState.Instance.RemainingTotalTime = Segments.Last().EndTimeSeconds - secondsSinceStart;
                AntManagerState.Instance.WorkoutMessage = CurrentMessage;
                if (nextSegment != null)
                {
                    AntManagerState.Instance.NextTargetPower = nextSegment.TargetRelativePowerIntensity * AntManagerState.Instance.CriticalPower;
                } else
                {
                    AntManagerState.Instance.NextTargetPower = 0;
                }
            }
        }

    }
}