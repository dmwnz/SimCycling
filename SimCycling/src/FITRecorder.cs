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
using System.Text;
using System.IO;
using System.Diagnostics;
using Dynastream.Fit;
using DateTime = Dynastream.Fit.DateTime;
using SimCycling.State;
using System.Linq;

namespace SimCycling
{
    class FITRecorder
    {
        static Encode encoder;
        static FileStream fitDest;
        static List<RecordMesg> records;

        static public void Start()
        {
            records = new List<RecordMesg>();

            var assettoFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Assetto Corsa\\SimCyclingActivities";
            if (!Directory.Exists(assettoFolder))
            {
                Directory.CreateDirectory(assettoFolder);
            }

            var filepath = assettoFolder + "\\" + System.DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".fit";


            // Create file encode object
            encoder = new Encode(ProtocolVersion.V20);

            fitDest = new FileStream(filepath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            encoder.Open(fitDest);

            var fileIdMesg = new FileIdMesg(); // Every FIT file MUST contain a 'File ID' message as the first message

            fileIdMesg.SetType(Dynastream.Fit.File.Activity);
            fileIdMesg.SetManufacturer(Manufacturer.Dynastream);  // Types defined in the profile are available
            fileIdMesg.SetProduct(22);
            fileIdMesg.SetSerialNumber(1234);
            fileIdMesg.SetTimeCreated(new DateTime(System.DateTime.Now));

            // Encode each message, a definition message is automatically generated and output if necessary
            encoder.Write(fileIdMesg);
        }

        public static void AddRecord()
        {
            var oldTimeStamp = records.Last().GetTimestamp().GetTimeStamp();
            var newTimeStamp = new DateTime(System.DateTime.Now).GetTimeStamp();
            if (newTimeStamp == oldTimeStamp)
            {
                return; // do not record twice with same timestamp ?
            }

            try
            {
                var newRecord = new RecordMesg();
                if (AntManagerState.GetInstance().CyclistHeartRate > 0)
                {
                    newRecord.SetHeartRate((byte)AntManagerState.GetInstance().CyclistHeartRate);
                }
                if (AntManagerState.GetInstance().BikeCadence > 0)
                {
                    newRecord.SetCadence((byte)AntManagerState.GetInstance().BikeCadence);
                }
                
                newRecord.SetPower((ushort)AntManagerState.GetInstance().CyclistPower);
                newRecord.SetGrade(AntManagerState.GetInstance().BikeIncline);
                newRecord.SetDistance(AntManagerState.GetInstance().TripTotalKm * 1000);
                newRecord.SetSpeed(AntManagerState.GetInstance().BikeSpeedKmh / 3.6f);
                newRecord.SetAltitude(RaceState.GetInstance().CarPositions[0].Y);
                newRecord.SetTimestamp(new DateTime(System.DateTime.Now));
                records.Add(newRecord);

            } catch (Exception e)
            {
                Console.Write("Failed to write record.");
                Console.WriteLine(e.Message);
            }

        }

        public static void Stop()
        {
            // Update header datasize and file CRC
            var sessionMesg = new SessionMesg();
            sessionMesg.SetSport(Sport.Cycling);
            sessionMesg.SetTotalDistance(AntManagerState.GetInstance().TripTotalKm * 1000);
            sessionMesg.SetTotalElapsedTime(AntManagerState.GetInstance().TripTotalTime);

            var activityMesg = new ActivityMesg();
            activityMesg.SetNumSessions(1);
            activityMesg.SetType(Activity.Manual);
            activityMesg.SetTotalTimerTime(AntManagerState.GetInstance().TripTotalTime);

            encoder.Write(records);
            encoder.Write(sessionMesg);
            encoder.Write(activityMesg);

            encoder.Close();
            fitDest.Close();
            
        }

    }
}
