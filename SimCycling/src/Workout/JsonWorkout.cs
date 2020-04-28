using System;
using System.Runtime.Serialization;
using System.Collections.Generic;
using SimCycling.State;

namespace SimCycling.Workout
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

        [DataMember(Name = "end_time")]
        public int EndTime { get; set; }

    }

    [DataContract]
    public class JsonWorkout : GenericWorkout
    {
        [DataMember(Name = "segments")]
        public new List<WorkoutSegment> Segments { get; set; }


        public int SegmentIndex(float t)
        {
            for (int i = 0; i < Segments.Count; i++)
            {
                if (Segments[i].StartTime <= t && Segments[i].EndTime > t)
                {
                    return i;
                }
            }
            return -1;
        }

        public new void Update()
        {
            AntManagerState state = AntManagerState.Instance;
            var t = state.TripTotalTime;
            int idx = SegmentIndex(t);
            if (idx == -1)
            {
                state.TargetPower = 0;
            }
            else
            {
                Console.WriteLine("Target power : {0}", Segments[idx].Power.Intensity);
                state.TargetPower = Segments[idx].Power.Intensity * state.CriticalPower;
                state.RemainingIntervalTime = Segments[idx].EndTime - t;
                state.RemainingTotalTime = Segments[Segments.Count - 1].EndTime - t;
                if (idx + 1 < Segments.Count)
                {
                    state.NextTargetPower = Segments[idx+1].Power.Intensity * state.CriticalPower;
                }
            }
        }
    }
}
