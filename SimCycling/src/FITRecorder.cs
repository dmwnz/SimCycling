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
//using System.DateTime;
using DateTime = Dynastream.Fit.DateTime;
using SimCycling.State;

namespace SimCycling
{
    class FITRecorder
    {
        static Encode encoder;
        static FileStream fitDest;
        static string filepath;
        static List<RecordMesg> records;

        static public void Start()
        {
            records = new List<RecordMesg>();

            var assettoFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Assetto Corsa\\SimCyclingActivities";
            if (!Directory.Exists(assettoFolder))
            {
                Directory.CreateDirectory(assettoFolder);
            }

            filepath = assettoFolder + "\\" + System.DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".fit";
        }

        public static void AddRecord()
        {

            try
            {
                var newRecord = new RecordMesg();
                if (AntManagerState.GetInstance().CyclistHeartRate > 0)
                {
                    newRecord.SetHeartRate((byte)(AntManagerState.GetInstance().CyclistHeartRate));
                }
                if (AntManagerState.GetInstance().BikeCadence > 0)
                {
                    newRecord.SetCadence((byte)(AntManagerState.GetInstance().BikeCadence));
                }
                if (AntManagerState.GetInstance().CyclistPower > 0)
                {
                    newRecord.SetPower((ushort)(AntManagerState.GetInstance().CyclistPower));
                }
                
                newRecord.SetDistance(AntManagerState.GetInstance().TripTotalKm * 1000);
                newRecord.SetSpeed((float)(AntManagerState.GetInstance().BikeSpeedKmh / 3.6));
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

            // Generate some FIT messages
            var fileIdMesg = new FileIdMesg(); // Every FIT file MUST contain a 'File ID' message as the first message

            byte[] appId = {
                1, 2, 3, 4,
                5, 6, 7, 8,
                9, 10, 11, 12,
                13, 14, 15, 16
            };

            fileIdMesg.SetType(Dynastream.Fit.File.Activity);
            fileIdMesg.SetManufacturer(Manufacturer.Dynastream);  // Types defined in the profile are available
            fileIdMesg.SetProduct(22);
            fileIdMesg.SetSerialNumber(1234);
            fileIdMesg.SetTimeCreated(new DateTime(System.DateTime.Now));

            // Write our header
            Console.WriteLine("writing fit");

            // Create file encode object
            encoder = new Encode(ProtocolVersion.V20);

            fitDest = new FileStream(filepath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            encoder.Open(fitDest);
            // Encode each message, a definition message is automatically generated and output if necessary
            encoder.Write(fileIdMesg);


            // Update header datasize and file CRC
            var sessionMesg = new SessionMesg();
            sessionMesg.SetSport(Sport.Cycling);
            sessionMesg.SetTotalDistance(AntManagerState.GetInstance().TripTotalKm * 1000);
            sessionMesg.SetTotalElapsedTime(AntManagerState.GetInstance().TripTotalTime);
            encoder.Write(sessionMesg);
            encoder.Write(records);


            encoder.Close();
            fitDest.Close();

        }

    }
}
