using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace SimCycling.Workout
{
    public class ZwiftWorkout : GenericWorkout
    {
        public ZwiftWorkout(ZwiftWorkoutFile zwiftFile)
        {
            Segments = zwiftFile.GetSegments();
        }
    }

    [XmlRoot("workout_file")]
    public class ZwiftWorkoutFile
    {
        public string author = "";
        public string name = "";
        public string description = "";
        public string sportType = "";
        
        [XmlArray]
        public List<Tag> tags;

        [XmlArray("workout")]
        [XmlArrayItem(typeof(Ramp))]
        [XmlArrayItem(typeof(SteadyState))]
        [XmlArrayItem(typeof(Warmup))]
        [XmlArrayItem(typeof(Cooldown))]
        [XmlArrayItem(typeof(IntervalsT))]
        [XmlArrayItem(typeof(FreeRide))]
        public List<WorkoutItem> workout;

        public List<Segment> GetSegments()
        {
            var res = new List<Segment>();
            var lastEndTime = 0.0f;
            for (int i = 0; i < workout.Count; i++)
            {
                var newSegments = workout[i].GetSegments(lastEndTime);
                res.AddRange(newSegments);
                lastEndTime = newSegments.Last().EndTimeSeconds;
            }
            return res;
        }
    }

    public class Tag
    {
        [XmlAttribute]
        public string name;
    }

    public abstract class WorkoutItem
    {
        [XmlAttribute]
        public int Duration;
        [XmlAttribute]
        public int Cadence;
        [XmlElement("textevent")]
        public List<TextEventItem> textevents;


        internal abstract List<Segment> GetSegments(float lastEndTime);
    }

    public class TextEventItem
    {
        [XmlAttribute]
        public int timeoffset;
        [XmlAttribute]
        public string message;
    }

    public class IntervalsT : WorkoutItem
    {
        [XmlAttribute]
        public int Repeat;
        [XmlAttribute]
        public int OnDuration;
        [XmlAttribute]
        public int OffDuration;
        [XmlAttribute]
        public float OnPower;
        [XmlAttribute]
        public float OffPower;

        internal override List<Segment> GetSegments(float previousSegmentEndTime)
        {
            var res = new List<Segment>();
            var lastEndTime = previousSegmentEndTime;

            for (int i = 0; i < Repeat; i++)
            {
                var onSegment = new Segment(lastEndTime, OnDuration, OnPower, Cadence, GetType());
                lastEndTime = onSegment.EndTimeSeconds;
                res.Add(onSegment);

                var offSegment = new Segment(lastEndTime, OffDuration, OffPower, Cadence, GetType());
                lastEndTime = onSegment.EndTimeSeconds;
                res.Add(offSegment);
            }
            return res;
        }
    }

    public abstract class Ramp : WorkoutItem
    {
        [XmlAttribute]
        public float PowerLow;
        [XmlAttribute]
        public float PowerHigh;

        private static readonly int RAMP_RESOLUTION = 60; //sec

        protected abstract float StartPower { get; }
        protected abstract float EndPower { get; }

        internal override List<Segment> GetSegments(float previousSegmentEndTime)
        {
            Duration = Math.Max(Duration, RAMP_RESOLUTION);

            //  5                    300        60
            var rampDiscretePoints = Duration / RAMP_RESOLUTION;
            var lastEndTime = previousSegmentEndTime;
            if (rampDiscretePoints == 1)
            {
                return new List<Segment> { new Segment(lastEndTime, Duration, StartPower, Cadence, GetType()) };
            }
            var res = new List<Segment>();
            for (int i = 0; i < rampDiscretePoints; i++)
            {
                var lerpFactor = (float)i / (float)(rampDiscretePoints - 1);
                var lerpPower = StartPower + lerpFactor * (EndPower - StartPower);
                var segment = new Segment(lastEndTime, RAMP_RESOLUTION, lerpPower, Cadence, GetType());
                res.Add(segment);
                lastEndTime = segment.EndTimeSeconds;
            }
            return res;

        }
    }

    public class SteadyState : WorkoutItem
    {
        [XmlAttribute]
        public float Power;

        internal override List<Segment> GetSegments(float lastEndTime)
        {
            return new List<Segment> { new Segment(lastEndTime, Duration, Power, Cadence, GetType()) };
        }
    }

    public class Cooldown : Ramp
    {
        protected override float StartPower => Math.Max(PowerLow, PowerHigh);
        protected override float EndPower => Math.Min(PowerLow, PowerHigh);
    }
    public class Warmup : Ramp
    {
        protected override float StartPower => Math.Min(PowerLow, PowerHigh);
        protected override float EndPower => Math.Max(PowerHigh, PowerLow);
    }

    public class FreeRide : WorkoutItem
    {
        [XmlAttribute]
        public bool FlatRoad;

        internal override List<Segment> GetSegments(float lastEndTime)
        {
            return new List<Segment> { new Segment(lastEndTime, Duration, 0, Cadence, GetType()) };
        }
    }

}
