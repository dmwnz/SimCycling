#region Copyright
////////////////////////////////////////////////////////////////////////////////
// The following FIT Protocol software provided may be used with FIT protocol
// devices only and remains the copyrighted property of Garmin Canada Inc.
// The software is being provided on an "as-is" basis and as an accommodation,
// and therefore all warranties, representations, or guarantees of any kind
// (whether express, implied or statutory) including, without limitation,
// warranties of merchantability, non-infringement, or fitness for a particular
// purpose, are specifically disclaimed.
//
// Copyright 2012 Garmin Canada Inc.
////////////////////////////////////////////////////////////////////////////////
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using Dynastream.Fit;
using DateTime = Dynastream.Fit.DateTime;
using SimCycling.State;
using System.Linq;

namespace SimCycling
{
    class FITRecorder
    {
        private static Encode encoder;
        private static FileStream fitDest;

        private static LapMesg currentLapMesg;
        private static SessionMesg sessionMesg;
        private static ActivityMesg activityMesg;
        
        private static uint lastRecordTimeStamp;
        private static float alreadyLappedDistance = 0.0f;
        private static ushort numLaps = 0;

        private static AntManagerState State => AntManagerState.GetInstance();

        static public void Start()
        {
            var assettoFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Assetto Corsa\\SimCyclingActivities";
            if (!Directory.Exists(assettoFolder))
            {
                Directory.CreateDirectory(assettoFolder);
            }

            var filepath = assettoFolder + "\\" + System.DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".fit";

            var now = new DateTime(System.DateTime.Now);

            // Create file encode object
            encoder = new Encode(ProtocolVersion.V20);

            fitDest = new FileStream(filepath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            encoder.Open(fitDest);

            var fileIdMesg = new FileIdMesg(); // Every FIT file MUST contain a 'File ID' message as the first message

            fileIdMesg.SetType(Dynastream.Fit.File.Activity);
            fileIdMesg.SetManufacturer(Manufacturer.Dynastream);  // Types defined in the profile are available
            fileIdMesg.SetProduct(22);
            fileIdMesg.SetSerialNumber(1234);
            fileIdMesg.SetTimeCreated(now);

            // Encode each message, a definition message is automatically generated and output if necessary
            encoder.Write(fileIdMesg);

            sessionMesg = new SessionMesg();
            sessionMesg.SetStartTime(now);

            currentLapMesg = new LapMesg();
            currentLapMesg.SetStartTime(now);
        }

        public static void AddRecord()
        {
            var now = new DateTime(System.DateTime.Now);
            if (lastRecordTimeStamp == now.GetTimeStamp())
            {
                return; // do not record twice with same timestamp
            }

            try
            {
                var newRecord = new RecordMesg();
                var hr = State.CyclistHeartRate > 0 ? (byte?)State.CyclistHeartRate : null;
                var cad = State.BikeCadence > 0 ? (byte?)State.BikeCadence : null;

                newRecord.SetTimestamp(now);

                newRecord.SetHeartRate(hr);
                newRecord.SetCadence(cad);
                newRecord.SetPower((ushort)State.CyclistPower);
                newRecord.SetGrade(State.BikeIncline);
                newRecord.SetDistance(State.TripTotalKm * 1000);
                newRecord.SetSpeed(State.BikeSpeedKmh / 3.6f);
                newRecord.SetAltitude(RaceState.GetInstance().CarPositions[0].Y);

                encoder.Write(newRecord);

                lastRecordTimeStamp = now.GetTimeStamp();

            } catch (Exception e)
            {
                Console.Write("Failed to write record.");
                Console.WriteLine(e.Message);
            }
        }

        public static void Stop()
        {
            var now = new DateTime(System.DateTime.Now);

            Lap();

            sessionMesg.SetTimestamp(now);
            sessionMesg.SetSport(Sport.Cycling);
            sessionMesg.SetSubSport(SubSport.VirtualActivity);
            sessionMesg.SetTotalDistance(State.TripTotalKm * 1000);
            sessionMesg.SetTotalElapsedTime(State.TripTotalTime);
            sessionMesg.SetFirstLapIndex(0);
            sessionMesg.SetNumLaps(numLaps);
            sessionMesg.SetEvent(Event.Session);
            sessionMesg.SetEventType(EventType.Stop);
            sessionMesg.SetEventGroup(0);

            activityMesg = new ActivityMesg();
            activityMesg.SetTimestamp(now);
            activityMesg.SetTotalTimerTime(State.TripTotalTime);
            activityMesg.SetNumSessions(1);
            activityMesg.SetType(Activity.Manual);
            activityMesg.SetEvent(Event.Activity);
            activityMesg.SetEventType(EventType.Stop);
            activityMesg.SetEventGroup(0);

            encoder.Write(sessionMesg);
            encoder.Write(activityMesg);

            encoder.Close();
            fitDest.Close();   
        }

        public static void Lap()
        {
            var now = new DateTime(System.DateTime.Now);
            currentLapMesg.SetTimestamp(now);
            currentLapMesg.SetSport(Sport.Cycling);
            currentLapMesg.SetTotalElapsedTime(now.GetTimeStamp() - currentLapMesg.GetStartTime().GetTimeStamp());
            currentLapMesg.SetTotalTimerTime(now.GetTimeStamp() - currentLapMesg.GetStartTime().GetTimeStamp());
            currentLapMesg.SetTotalDistance(State.TripTotalKm * 1000 - alreadyLappedDistance);
            currentLapMesg.SetEvent(Event.Lap);
            currentLapMesg.SetEventType(EventType.Stop);
            currentLapMesg.SetEventGroup(0);

            encoder.Write(currentLapMesg);

            numLaps++;
            currentLapMesg = new LapMesg();
            alreadyLappedDistance = State.TripTotalKm * 1000;

            currentLapMesg.SetStartTime(now);
        }
    }
}
