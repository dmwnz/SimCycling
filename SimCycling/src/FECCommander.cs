using System;
using System.Text;
using SimCycling.Utils;
using AssettoCorsaSharedMemory;
using vJoyInterfaceWrap;
using System.IO.MemoryMappedFiles;
using System.IO;
using AntPlus.Profiles.Common;
using System.Globalization;
using AntPlus.Profiles.FitnessEquipment;
using System.Configuration;

namespace SimCycling
{
    class FECCommander
    {
        float transmittedGrade = 0.0f;
        long lastTransmittedGradeTime = 0L;
        // bool acquiredVJoy = false;
        Point3D lastCoordinates = new Point3D(0.0f, 0.0f, 0.0f);
        int lastPower = 0;
        AssettoCorsa ac;
        PID pid;
        uint idVJoy;
        vJoy joystick;
        bool acquired;

        MemoryMappedFile mm;
        MemoryMappedFile mmHr;
        MemoryMappedFile mmCad;

        StreamWriter gpxFile;
        StreamWriter posFile;

        bool equipmentFound;
        string track;

        float speedKmh;

        FitnessEquipmentDisplay simulator;

        public FECCommander(FitnessEquipmentDisplay simulator)
        {
            this.simulator = simulator;
        }
        
        public static void Log(String s, params object[] parms)
        {
            Console.WriteLine(s, parms);
        }

        public void Start()
        {
            simulator.SensorFound += Found;

            simulator.CommandStatusPageReceived += OnPageCommandStatus;
            simulator.FeCapabilitiesPageReceived += OnPageFeCapabilities;
            simulator.GeneralFePageReceived += OnPageGeneralFE;
            simulator.SpecificTrainerPageReceived += OnPageSpecificTrainer;

            simulator.TurnOn();

            InitMmap();
            InitVJoy();
            InitAC();
            InitGPXFile();
        }

        public void Stop()
        {
            simulator.SensorFound -= Found;

            simulator.CommandStatusPageReceived -= OnPageCommandStatus;
            simulator.FeCapabilitiesPageReceived -= OnPageFeCapabilities;
            simulator.GeneralFePageReceived -= OnPageGeneralFE;
            simulator.SpecificTrainerPageReceived -= OnPageSpecificTrainer;

            if (acquired)
            {
                joystick.RelinquishVJD(idVJoy);
            }

            ac.PhysicsUpdated -= OnACPhysics;
            ac.GraphicsUpdated -= OnACGraphics;
            ac.StaticInfoUpdated -= OnACInfo;
            ac.Stop();



            gpxFile.WriteLine(@"    </trkseg>
  </trk>
</gpx> ");
            gpxFile.Close();

            posFile.Close();

            mmHr.Dispose();
            mmCad.Dispose();
            mm.Dispose();

            simulator.TurnOff();
        }

        private void InitMmap()
        {
            //var hrFilePath = String.Format(@"{0}\hr.bin", Consts.BASE_OUT_PATH);
            mmHr = MemoryMappedFile.OpenExisting("hr.bin");
            //var cadFilePath = String.Format(@"{0}\cad.bin", Consts.BASE_OUT_PATH);
            mmCad = MemoryMappedFile.OpenExisting("cad.bin");
            //var mmFilePath = String.Format(@"{0}\mm.bin", Consts.BASE_OUT_PATH);
            mm = MemoryMappedFile.CreateOrOpen("mm.bin", 32);
        }
        
        private void InitVJoy()
        {
            //myLog("Init vJoy")

            idVJoy = 1u;
            joystick = new vJoy();

            var enabled = joystick.vJoyEnabled();
            if (!enabled)
            {
                Log("vJoy driver not enabled: Failed Getting vJoy attributes.");
                return;
            }

            //myLog("Vendor: {0}\nProduct :{1}\nVersion Number:{2}\n".format(\
            //self._joystick.GetvJoyManufacturerString(), \
            //self._joystick.GetvJoyProductString(), \
            //self._joystick.GetvJoySerialNumberString()))

            var vjdStatus = joystick.GetVJDStatus(idVJoy);
            Log(vjdStatus.ToString());
            switch (vjdStatus)
            {
                case VjdStat.VJD_STAT_OWN:
                    Log("vJoy Device {0} is already owned by this feeder\n", idVJoy);
                    break;
                case VjdStat.VJD_STAT_FREE:
                    Log("vJoy Device {0} is free\n", idVJoy);
                    break;
                case VjdStat.VJD_STAT_BUSY:
                    Log("vJoy Device {0} is already owned by another feeder\nCannot continue\n", idVJoy);
                    return;
                case VjdStat.VJD_STAT_MISS:
                    Log("vJoy Device {0} is not installed or disabled\nCannot continue\n", idVJoy);
                    return;
                default:
                    Log("vJoy Device {0} general error\nCannot continue\n", idVJoy);
                    return;
            }


            acquired = joystick.AcquireVJD(idVJoy);
            if (!acquired)
            {
                Log("Failed to acquire vJoy device number {0}.\n", idVJoy);
                return;
            }
            Log("Acquired: vJoy device number {0}.\n", idVJoy);
        }

        private void InitAC()
        {
            Log("Init Ac");
            ac = new AssettoCorsa
            {
                PhysicsInterval = 100,
                GraphicsInterval = 1000,
                StaticInfoInterval = 1000
            };

            ac.PhysicsUpdated += OnACPhysics;
            ac.GraphicsUpdated += OnACGraphics;
            ac.StaticInfoUpdated += OnACInfo;
            

            ac.Start();
            transmittedGrade = 999f;


            Log("AC Is running : " + ac.IsRunning);


            var P = 0.3f;
            var I = 0.005f;
            var D = 0.0f;

            pid = new PID(P, I, D);
            pid.Clear();
        }

        private void InitGPXFile()
        {
            var title = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            posFile = new StreamWriter(String.Format(@"{0}\out-{1}.csv", Consts.BASE_OUT_PATH, title), false, Encoding.UTF8, 1024);
            gpxFile = new StreamWriter(String.Format(@"{0}\out-{1}.gpx", Consts.BASE_OUT_PATH, title), false, Encoding.UTF8, 1024);

            gpxFile.WriteLine(String.Format(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<gpx xmlns=""http://www.topografix.com/GPX/1/1""
  xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
  xsi:schemaLocation=""http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd http://www.garmin.com/xmlschemas/GpxExtensions/v3 http://www.garmin.com/xmlschemas/GpxExtensionsv3.xsd http://www.garmin.com/xmlschemas/TrackPointExtension/v1 http://www.garmin.com/xmlschemas/TrackPointExtensionv1.xsd http://www.garmin.com/xmlschemas/GpxExtensions/v3 http://www.garmin.com/xmlschemas/GpxExtensionsv3.xsd http://www.garmin.com/xmlschemas/TrackPointExtension/v1 http://www.garmin.com/xmlschemas/TrackPointExtensionv1.xsd""
  xmlns:gpxtpx=""http://www.garmin.com/xmlschemas/TrackPointExtension/v1""
  xmlns:gpxx=""http://www.garmin.com/xmlschemas/GpxExtensions/v3""
  creator=""SimCycling"" version=""1.1"">
  <trk>
    <name>{0}</name>
    <trkseg>", title));

        }

        private void OnACPhysics(object sender, PhysicsEventArgs e)
        {
            //#myLog("On Ac Physics")

            //#myLog("AC Speed : {0}".format(e.Physics.SpeedKmh))
            var acSpeed = e.Physics.SpeedKmh;
            pid.Update(acSpeed);
            var coeff = pid.Output;


            if (acquired)
            {
                var axisVal = (int)Math.Round(coeff * 16384) + 16383;
                //# myLog("SetAxis " + str(axisVal));
                joystick.SetAxis(axisVal, idVJoy, HID_USAGES.HID_USAGE_X);
            }
        }

        private void OnACGraphics(object sender, GraphicsEventArgs e)
        {
            Log("On AC Graphics");
            var xCoord = e.Graphics.CarCoordinates[0];
            var zCoord = e.Graphics.CarCoordinates[1];
            var yCoord = e.Graphics.CarCoordinates[2];
            Log(String.Format("Car coordinates : {0}, {1}, {2}", xCoord, yCoord, zCoord));
            var coordinates = new Point3D(xCoord, yCoord, zCoord);


            var altitudeDiff = coordinates.Z - lastCoordinates.Z;


            var distance = Consts.CalcDistance(lastCoordinates, coordinates);

            var newPitch = transmittedGrade;
            if (distance > 0.01)
            {
                newPitch = (float)Math.Round(altitudeDiff * 1000.0f / distance) / 10.0f;
            }

            lastCoordinates = coordinates;
            WriteGPXLine();
            Log(String.Format("newPitch : {0}", newPitch));

            if (equipmentFound)
            {
                if (DateTimeOffset.Now.ToUnixTimeMilliseconds() > lastTransmittedGradeTime + 2)
                {
                    Log(String.Format("sending new pitch {0}", newPitch));
                    transmittedGrade = newPitch;
                    SendTrackResistance(newPitch);
                    lastTransmittedGradeTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                }
            }
        }

        private void WriteGPXLine()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            posFile.Write(String.Format("{0};{1};{2}\n", lastCoordinates.X, lastCoordinates.Y, lastCoordinates.Z).Replace(".", ","));

            var wgsCoord = Consts.XYZToWGS(lastCoordinates, track);
            Log(String.Format("WGS : {0}, {1}, {2}", wgsCoord.Latitude, wgsCoord.Longitude, wgsCoord.Elevation));

            
            var hrAccessor = mmHr.CreateViewAccessor();
            char[] readHr = new char[32];
            hrAccessor.ReadArray(0, readHr, 0, 32);
            hrAccessor.Dispose();
            var readHrUtf8 = new String(readHr);

            int.TryParse(readHrUtf8.Split('|')[0], out int hr);
            Log(String.Format("HR {0}", hr));

            var cadAccessor = mmCad.CreateViewAccessor();
            char[] readCad = new char[32];
            cadAccessor.ReadArray(0, readCad, 0, 32);
            cadAccessor.Dispose();
            var readCadUtf8 = new String(readCad);

            int.TryParse(readCadUtf8.Split('|')[0], out int cad);
            Log(String.Format("Cad {0}", cad));


            var extensions = "        <extensions>\n";
            extensions += String.Format("          <power>{0}</power>\n", lastPower);
            if (hr != 0 || cad != 0)
            {
                extensions += "          <gpxtpx:TrackPointExtension>\n";
                if (hr != 0)
                {
                    extensions += String.Format("            <gpxtpx:hr>{0}</gpxtpx:hr>\n", hr);
                }
                if (cad != 0)
                {
                    extensions += String.Format("            <gpxtpx:cad>{0}</gpxtpx:hr>\n", cad);
                }
                extensions += "          </gpxtpx:TrackPointExtension>\n";
            }
            extensions += "        </extensions>";

            var toWrite = String.Format(@"      <trkpt lon=""{0:0.000000}"" lat=""{1:0.000000}"" >
        <ele>{2:0.00}</ele>
        <time>{3:s}</time>
{4}
      </trkpt>",
            wgsCoord.Longitude,
            wgsCoord.Latitude,
            wgsCoord.Elevation,
            DateTime.Now,
            extensions);
            gpxFile.WriteLine(toWrite);
        }

        private void OnACInfo(object sender, StaticInfoEventArgs e)
        {
            track = e.StaticInfo.Track;
            Log("TRACK : " + track);
        }

        private void Found(ushort a, byte b)
        {
            Log("Bkool found !");
            equipmentFound = true;

            RequestCommandStatus();
        }

        private void OnPageGeneralFE(GeneralFePage page, uint counter)
        {            
            speedKmh = page.Speed * 0.0036f;

            pid.SetPoint = speedKmh;
        }

        private void OnPageSpecificTrainer(SpecificTrainerPage page, uint counter)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;


            lastPower = page.InstantaneousPower;
            var mmAccessor = mm.CreateViewAccessor();
            var data = String.Format("{0:0000.00}|{1:0000.00}|{2:0000.00}|",
                page.InstantaneousPower,
                pid.SetPoint,
                transmittedGrade).ToCharArray();

            mmAccessor.WriteArray(0, data, 0, data.Length);
            mmAccessor.Dispose();

            
        }

        private void OnPageFeCapabilities(FeCapabilitiesPage page, uint counter)
        {
            Log("Basic resistance " +
                page.FeCapabilities.BasicResistanceModeSupported);
            Log("Power " +
                page.FeCapabilities.TargetPowerModeSupported);
            Log("Simulation " +
                page.FeCapabilities.SimulationModeSupported);
        }

        private void OnPageCommandStatus(CommandStatusPage page, uint counter)
        {
            Log("status : " + page.LastReceivedCommandID);
            SendUserConfiguration();
        }

        private void SendUserConfiguration()
        {
            var bikeWeight = float.Parse(ConfigurationManager.AppSettings["bikeweight"]);
            var riderWeight = float.Parse(ConfigurationManager.AppSettings["riderweight"]);

            // 170//8.5kg
            // 6250//62.5kg
            var command = new UserConfigurationPage
            {
                BikeWeight = (ushort)(bikeWeight*20), 
                UserWeight = (ushort)(riderWeight*100), 
                WheelDiameter = 62
            };
            simulator.SendUserConfiguration(command);
        }


        private void SendTargetPower()
        {
            var command = new ControlTargetPowerPage
            {
                TargetPower = 30 // # 7.5 W
            };
            simulator.SendTargetPower(command);
        }

        private void SendWindResistance()
        {
            var command = new ControlWindResistancePage
            {
                WindResistanceCoefficient = 0xFF, //# Invald
                WindSpeed = 255, //# Invalid
                DraftingFactor = 10 //# 0.1
            };
            simulator.SendWindResistance(command);
        }

        private void SendTrackResistance(float grade)
        {
            var gradeToTransmit = Consts.ConvertGrade(grade);
            var command = new ControlTrackResistancePage
            {
                Grade = gradeToTransmit
            };
            simulator.SendTrackResistance(command);
        }

        private void RequestCommandStatus()
        {
            var request = new RequestDataPage
            {
                RequestedPageNumber = 0x47 //  # Command Status page (0x47)
            };
            simulator.SendDataPageRequest(request);
        }

        private void RequestFECapabilities()
        {
            var request = new RequestDataPage
            {
                RequestedPageNumber = 0x36  //# FE Capabilities page (0x36)
            };
            simulator.SendDataPageRequest(request);
        }
    }
}
