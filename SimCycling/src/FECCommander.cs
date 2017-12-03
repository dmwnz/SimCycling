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

namespace SimCycling
{
    class FECCommander
    {
        float transmittedGrade = 0.0f;
        long lastTransmittedGradeTime = 0L;
        bool acquiredVJoy = false;
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

        bool equipment_found;
        string track;

        float speedKmh;

        FitnessEquipmentDisplay simulator;

        public FECCommander(FitnessEquipmentDisplay simulator)
        {
            this.simulator = simulator;
            simulator.SensorFound += found;
            simulator.GeneralFePageReceived += on_page_general_fe;
            simulator.SpecificTrainerPageReceived += on_page_specific_trainer;
            simulator.FeCapabilitiesPageReceived += on_page_fe_capabilities;
            simulator.CommandStatusPageReceived += on_page_command_status;
        }

        ~FECCommander()
        {
            simulator.SensorFound -= found;
            simulator.GeneralFePageReceived -= on_page_general_fe;
            simulator.SpecificTrainerPageReceived -= on_page_specific_trainer;
            simulator.FeCapabilitiesPageReceived -= on_page_fe_capabilities;
            simulator.CommandStatusPageReceived -= on_page_command_status;
        }

        public static void myLog(String s, params object[] parms)
        {
            Console.WriteLine(s, parms);
        }

        public void Start()
        {
            initMmap();
            initVjoy();
            initAC();
            initGPX();
        }

        public void Finish()
        {
            simulator.CommandStatusPageReceived -= on_page_command_status;
            simulator.FeCapabilitiesPageReceived -= on_page_fe_capabilities;
            simulator.GeneralFePageReceived -= on_page_general_fe;
            simulator.SpecificTrainerPageReceived -= on_page_specific_trainer;

            if (acquired)
            {
                joystick.RelinquishVJD(idVJoy);
            }

            ac.PhysicsUpdated -= on_ac_physics;
            ac.GraphicsUpdated -= on_ac_graphics;
            ac.StaticInfoUpdated -= on_ac_info;
            ac.Stop();



            gpxFile.WriteLine(@"  </trkseg>
    </trk>
</gpx> ");
            gpxFile.Close();

            posFile.Close();

            mmHr.Dispose();
            mmCad.Dispose();
            mm.Dispose();
        }

        private void initMmap()
        {
            var hrFilePath = String.Format(@"{0}\hr.bin", Consts.BASE_OUT_PATH);
            mmHr = MemoryMappedFile.CreateFromFile(hrFilePath, FileMode.Open, "hr.bin");
            var cadFilePath = String.Format(@"{0}\cad.bin", Consts.BASE_OUT_PATH);
            mmCad = MemoryMappedFile.CreateFromFile(cadFilePath, FileMode.Open, "cad.bin");
            var mmFilePath = String.Format(@"{0}\mm.bin", Consts.BASE_OUT_PATH);
            mm = MemoryMappedFile.CreateFromFile(mmFilePath, FileMode.Create, "mm.bin", 32);
        }

        private void initVjoy()
        {
            //myLog("Init vJoy")

            idVJoy = 1u;
            joystick = new vJoy();

            var enabled = joystick.vJoyEnabled();
            if (!enabled)
            {
                myLog("vJoy driver not enabled: Failed Getting vJoy attributes.");
                return;
            }

            //myLog("Vendor: {0}\nProduct :{1}\nVersion Number:{2}\n".format(\
            //self._joystick.GetvJoyManufacturerString(), \
            //self._joystick.GetvJoyProductString(), \
            //self._joystick.GetvJoySerialNumberString()))

            var vjdStatus = joystick.GetVJDStatus(idVJoy);
            myLog(vjdStatus.ToString());
            switch (vjdStatus)
            {
                case VjdStat.VJD_STAT_OWN:
                    myLog("vJoy Device {0} is already owned by this feeder\n", idVJoy);
                    break;
                case VjdStat.VJD_STAT_FREE:
                    myLog("vJoy Device {0} is free\n", idVJoy);
                    break;
                case VjdStat.VJD_STAT_BUSY:
                    myLog("vJoy Device {0} is already owned by another feeder\nCannot continue\n", idVJoy);
                    return;
                case VjdStat.VJD_STAT_MISS:
                    myLog("vJoy Device {0} is not installed or disabled\nCannot continue\n", idVJoy);
                    return;
                default:
                    myLog("vJoy Device {0} general error\nCannot continue\n", idVJoy);
                    return;
            }


            acquired = joystick.AcquireVJD(idVJoy);
            if (!acquired)
            {
                myLog("Failed to acquire vJoy device number {0}.\n", idVJoy);
                return;
            }
            myLog("Acquired: vJoy device number {0}.\n", idVJoy);
        }

        private void initAC()
        {
            myLog("Init Ac");
            ac = new AssettoCorsa();
            ac.PhysicsInterval = 100;
            ac.PhysicsUpdated += on_ac_physics;
            ac.GraphicsInterval = 1000;
            ac.GraphicsUpdated += on_ac_graphics;
            ac.StaticInfoInterval = 1000;
            ac.StaticInfoUpdated += on_ac_info;

            ac.Start();
            this.transmittedGrade = 999f;


            myLog("AC Is running : " + ac.IsRunning);


            var P = 0.3f;
            var I = 0.005f;
            var D = 0.0f;

            pid = new PID(P, I, D);
            pid.clear();
        }

        private void initGPX()
        {
            var title = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            posFile = new StreamWriter(String.Format(@"{0}\bkool-{1}.csv", Consts.BASE_OUT_PATH, title), false, Encoding.UTF8, 1024);
            gpxFile = new StreamWriter(String.Format(@"{0}\bkool-{1}.gpx", Consts.BASE_OUT_PATH, title), false, Encoding.UTF8, 1024);

            gpxFile.WriteLine(String.Format(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<gpx xmlns=""http://www.topografix.com/GPX/1/1""
  xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
  xsi:schemaLocation=""http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd http://www.garmin.com/xmlschemas/GpxExtensions/v3 http://www.garmin.com/xmlschemas/GpxExtensionsv3.xsd http://www.garmin.com/xmlschemas/TrackPointExtension/v1 http://www.garmin.com/xmlschemas/TrackPointExtensionv1.xsd http://www.garmin.com/xmlschemas/GpxExtensions/v3 http://www.garmin.com/xmlschemas/GpxExtensionsv3.xsd http://www.garmin.com/xmlschemas/TrackPointExtension/v1 http://www.garmin.com/xmlschemas/TrackPointExtensionv1.xsd""
  xmlns:gpxtpx=""http://www.garmin.com/xmlschemas/TrackPointExtension/v1""
  xmlns:gpxx=""http://www.garmin.com/xmlschemas/GpxExtensions/v3""
  creator=""AssettoStrada"" version=""1.1"">
  <trk>
    <name>{0}</name>
    <trkseg>", title));

        }

        private void on_ac_physics(object sender, PhysicsEventArgs e)
        {
            //#myLog("On Ac Physics")

            //#myLog("AC Speed : {0}".format(e.Physics.SpeedKmh))
            var acSpeed = e.Physics.SpeedKmh;
            pid.update(acSpeed);
            var coeff = pid.output;


            if (acquired)
            {
                var axisVal = (int)Math.Round(coeff * 16384) + 16383;
                //# myLog("SetAxis " + str(axisVal));
                joystick.SetAxis(axisVal, idVJoy, HID_USAGES.HID_USAGE_X);
            }
        }

        private void on_ac_graphics(object sender, GraphicsEventArgs e)
        {
            myLog("On AC Graphics");
            var xCoord = e.Graphics.CarCoordinates[0];
            var zCoord = e.Graphics.CarCoordinates[1];
            var yCoord = e.Graphics.CarCoordinates[2];
            myLog(String.Format("Car coordinates : {0}, {1}, {2}", xCoord, yCoord, zCoord));
            var coordinates = new Point3D(xCoord, yCoord, zCoord);


            var altitudeDiff = coordinates.Z - lastCoordinates.Z;


            var distance = Consts.CalcDistance(lastCoordinates, coordinates);

            var newPitch = transmittedGrade;
            if (distance > 0.01)
            {
                newPitch = (float)Math.Round(altitudeDiff * 1000.0f / distance) / 10.0f;
            }

            lastCoordinates = coordinates;
            write_gpx_line();
            myLog(String.Format("newPitch : {0}", newPitch));

            if (equipment_found)
            {
                if (DateTimeOffset.Now.ToUnixTimeMilliseconds() > lastTransmittedGradeTime + 2)
                {
                    myLog(String.Format("sending new pitch {0}", newPitch));
                    transmittedGrade = newPitch;
                    send_track_resistance(newPitch);
                    lastTransmittedGradeTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                }
            }
        }

        private void write_gpx_line()
        {
            posFile.Write(String.Format("{0};{1};{2}\n", lastCoordinates.X, lastCoordinates.Y, lastCoordinates.Z).Replace(".", ","));

            var wgsCoord = Consts.XYZToWGS(lastCoordinates, track);
            myLog(String.Format("WGS : {0}, {1}, {2}", wgsCoord.Latitude, wgsCoord.Longitude, wgsCoord.Elevation));



            var hrAccessor = mmHr.CreateViewAccessor();
            char[] readHr = new char[32];
            hrAccessor.ReadArray(0, readHr, 0, 32);
            hrAccessor.Dispose();
            var readHrUtf8 = new String(readHr);

            var hr = int.Parse(readHrUtf8.Split('|')[0]);
            myLog(String.Format("HR {0}", hr));

            var cadAccessor = mmCad.CreateViewAccessor();
            char[] readCad = new char[32];
            cadAccessor.ReadArray(0, readCad, 0, 32);
            cadAccessor.Dispose();
            var readCadUtf8 = new String(readCad);

            var cad = int.Parse(readCadUtf8.Split('|')[0]);
            myLog(String.Format("Cad {0}", cad));


            var extensions = "    <extensions>\n";
            extensions += String.Format("     <power>{0}</power>\n", lastPower);
            if (hr != 0 || cad != 0)
            {
                extensions += "     <gpxtpx:TrackPointExtension>\n";
                if (hr != 0)
                {
                    extensions += String.Format("      <gpxtpx:hr>{0}</gpxtpx:hr>\n", hr);
                }
                if (cad != 0)
                {
                    extensions += String.Format("      <gpxtpx:cad>{0}</gpxtpx:hr>\n", cad);
                }
                extensions += "     </gpxtpx:TrackPointExtension>\n";
                extensions += "    </extensions>";
            }
            gpxFile.WriteLine(String.Format(@"   <trkpt lon=""{0:.6f}"" lat=""{1:.6f}"" >
    <ele>{2:.2f}</ele>
    <time>{3:s}</time>
{4}
   </trkpt>",
            wgsCoord.Longitude,
            wgsCoord.Latitude,
            wgsCoord.Elevation),
            DateTime.Now,
            extensions);
        }

        private void on_ac_info(object sender, StaticInfoEventArgs e)
        {
            track = e.StaticInfo.Track;
            myLog("TRACK : " + track);
        }

        private void found(ushort a, byte b)
        {
            myLog("Bkool trouve");
            equipment_found = true;

            simulator.CommandStatusPageReceived += on_page_command_status;
            simulator.FeCapabilitiesPageReceived += on_page_fe_capabilities;
            simulator.GeneralFePageReceived += on_page_general_fe;
            simulator.SpecificTrainerPageReceived += on_page_specific_trainer;

            request_command_status();
        }

        private void on_page_general_fe(GeneralFePage page, uint counter)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            {
                var hrAccessor = mm.CreateViewAccessor();
                char[] readHr = new char[32];
                hrAccessor.ReadArray(0, readHr, 0, 32);
                hrAccessor.Dispose();
                //var readHrUtf8 = new String(readHr, 0, 32, System.Text.Encoding.UTF8);
                Console.WriteLine(readHr);
            }

            speedKmh = page.Speed * 0.0036f;

            pid.SetPoint = speedKmh;
        }

        private void on_page_specific_trainer(SpecificTrainerPage page, uint counter)
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

        private void on_page_fe_capabilities(FeCapabilitiesPage page, uint counter)
        {
            myLog("Basic resistance " +
                page.FeCapabilities.BasicResistanceModeSupported);
            myLog("Power " +
                page.FeCapabilities.TargetPowerModeSupported);
            myLog("Simulation " +
                page.FeCapabilities.SimulationModeSupported);
        }

        private void on_page_command_status(CommandStatusPage page, uint counter)
        {
            myLog("status : " + page.LastReceivedCommandID);
            send_user_configuration();
        }

        private void send_user_configuration()
        {
            var command = new UserConfigurationPage();
            command.BikeWeight = 170; //8.5kg
            command.UserWeight = 6250; //62.5kg
            command.WheelDiameter = 62;
            simulator.SendUserConfiguration(command);
        }


        private void send_target_power()
        {
            var command = new ControlTargetPowerPage();
            command.TargetPower = 30; // # 7.5 W
            simulator.SendTargetPower(command);
        }

        private void send_wind_resistance()
        {
            var command = new ControlWindResistancePage();
            command.WindResistanceCoefficient = 0xFF; //# Invald
            command.WindSpeed = 255; //# Invalid
            command.DraftingFactor = 10; //# 0.1
            simulator.SendWindResistance(command);
        }

        private void send_track_resistance(float grade)
        {
            var command = new ControlTrackResistancePage();
            var gradeToTransmit = Consts.ConvertGrade(grade);
            command.Grade = gradeToTransmit;
            simulator.SendTrackResistance(command);
        }

        private void request_command_status()
        {
            var request = new RequestDataPage();
            request.RequestedPageNumber = 0x47; //  # Command Status page (0x47)
            simulator.SendDataPageRequest(request);
        }

        private void request_fe_capabilities()
        {
            var request = new RequestDataPage();
            request.RequestedPageNumber = 0x36;  //# FE Capabilities page (0x36)
            simulator.SendDataPageRequest(request);
        }


    }
}
