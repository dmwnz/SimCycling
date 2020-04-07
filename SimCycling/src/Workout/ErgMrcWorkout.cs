using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SimCycling.Workout
{
    internal class ErgMrcWorkout : GenericWorkout
    {
        public int Version { get; private set; }
        public string Units { get; private set; }
        public string Description { get; private set; }
        public string FileName { get; private set; }
        public float FTP { get; private set; }
        public TimeUnits TimeUnit { get; private set; }
        public PowerUnits PowerUnit { get; private set; }

        enum ParsingState
        {
            NONE,
            COURSE_HEADER,
            COURSE_DATA
        }

        public enum TimeUnits : int
        {
            SECONDS = 1,
            MINUTES = 60,
            HOURS = 3600
        }

        public enum PowerUnits
        {
            WATTS,
            PERCENTAGE
        }
        public static ErgMrcWorkout LoadFromFile(string filename)
        {
            var res = new ErgMrcWorkout();

            var parsingState = ParsingState.NONE;

            var lines = File.ReadAllLines(filename);
            var lastStartTimeSeconds = 0.0f;
            var lastTargetRelativePower = 0.0f;

            for (int i = 0; i < lines.Length; i++)
            {
                if(parsingState == ParsingState.NONE && lines[i].Equals("[COURSE HEADER]"))
                {
                    parsingState = ParsingState.COURSE_HEADER;
                    continue;
                }
                if (parsingState == ParsingState.COURSE_HEADER && lines[i].Equals("[END COURSE HEADER]"))
                {
                    parsingState = ParsingState.NONE;
                    continue;
                }
                if (parsingState == ParsingState.NONE && lines[i].Equals("[COURSE DATA]"))
                {
                    parsingState = ParsingState.COURSE_DATA;
                    continue;
                }
                if (parsingState == ParsingState.COURSE_DATA && lines[i].Equals("[END COURSE DATA]"))
                {
                    parsingState = ParsingState.NONE;
                    continue;
                }

                if (parsingState == ParsingState.COURSE_HEADER)
                {
                    var keyVal = lines[i].Split('=');
                    if(keyVal.Length == 1)
                    {
                        keyVal = keyVal[0].Split(' ', '\t');
                        var timeUnit = keyVal[0];
                        var powerUnit = keyVal[1];
                        res.TimeUnit = (TimeUnits)Enum.Parse(typeof(TimeUnits), timeUnit);
                        res.PowerUnit = (PowerUnits)Enum.Parse(typeof(PowerUnits), powerUnit);
                        continue;
                    }
                    var key = keyVal[0].Trim();
                    var val = keyVal[1].Trim();

                    switch (key)
                    {
                        case "VERSION":
                            res.Version = int.Parse(val);
                            break;
                        case "UNITS":
                            res.Units = val;
                            break;
                        case "DESCRIPTION":
                            res.Description = val;
                            break;
                        case "FILE NAME":
                            res.FileName = val;
                            break;
                        case "FTP":
                            res.FTP = float.Parse(val);
                            break;
                    }

                    continue;
                }

                if (parsingState == ParsingState.COURSE_DATA)
                {

                    var keyVal = lines[i].Split(' ', '\t');
                    var startTimeRaw = float.Parse(keyVal[0]);
                    var startTimeSeconds = startTimeRaw * (int)res.TimeUnit;
                    var powerRaw = float.Parse(keyVal[1]);
                    var targetRelativePower = res.FTP > 0 ? powerRaw / res.FTP : powerRaw / 100;

                    if (lastTargetRelativePower > 0 && startTimeSeconds > lastStartTimeSeconds)
                    {
                        var segment = new Segment(lastStartTimeSeconds, startTimeSeconds - lastStartTimeSeconds, lastTargetRelativePower, 0, typeof(Segment));
                        res.Segments.Add(segment);
                    }

                    lastTargetRelativePower = targetRelativePower;
                    lastStartTimeSeconds = startTimeSeconds;

                    continue;
                }

            }

            return res;
        }

    }
}