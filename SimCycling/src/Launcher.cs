using System;
using ANT_Managed_Library;
using AntPlus.Profiles.HeartRate;
using AntPlus.Profiles.FitnessEquipment;
using AntPlus.Profiles.BikeCadence;
using System.Collections.Generic;
using SimCycling.Utils;
using AssettoCorsaSharedMemory;
using vJoyInterfaceWrap;
using System.IO.MemoryMappedFiles;
using System.IO;
using AntPlus.Profiles.Common;

namespace SimCycling
{

    class Launcher
    {
        static void Main(string[] args)
        {
            ANTDeviceManager manager = new ANTDeviceManager();
            manager.Start();

        }
    }

    class ANTDeviceManager
    {
        static readonly byte[] NETWORK_KEY = { 0xB9, 0xA5, 0x21, 0xFB, 0xBD, 0x72, 0xC3, 0x45 }; // ANT+ Managed network key
        static readonly byte CHANNEL_FREQUENCY = 0x39;

        ANT_Device usbDevice;

        HeartRateDisplay heartRateDisplay;
        FitnessEquipmentDisplay fitnessEquipmentDisplay;
        BikeCadenceDisplay bikeCadenceDisplay;

        AntPlus.Types.Network network;

        public void Start()
        {
            try
            {
                usbDevice = new ANT_Device();
                usbDevice.ResetSystem();
                usbDevice.setNetworkKey(0, NETWORK_KEY);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.Read();
                return;
            }

            network = new AntPlus.Types.Network(0, NETWORK_KEY, CHANNEL_FREQUENCY);

            InitHRM(0);
            InitFEC(1);
            InitCAD(2);



            Console.Read();
            heartRateDisplay.TurnOff();
        }

        void InitHRM(int channelNumber)
        {
            var channelHrm = usbDevice.getChannel(channelNumber);
            heartRateDisplay = new HeartRateDisplay(channelHrm, network);
            heartRateDisplay.TurnOn();
            heartRateDisplay.SensorFound += (a, b) => Console.WriteLine("Heart Rate Sensor Found!");
            heartRateDisplay.HeartRateDataReceived += (a, b) => Console.WriteLine("HR Data : " + a.HeartRate);
        }

        void InitFEC(int channelNumber)
        {
            var channelFec = usbDevice.getChannel(channelNumber);

            fitnessEquipmentDisplay = new FitnessEquipmentDisplay(channelFec, network);
            fitnessEquipmentDisplay.TurnOn();
            fitnessEquipmentDisplay.SensorFound += (a, b) => Console.WriteLine("FE Sensor Found!");
            fitnessEquipmentDisplay.GeneralFePageReceived += (a, b) => Console.WriteLine("FE Data : " + a.Speed);
        }

        void InitCAD(int channelNumber)
        {
            var channelCad = usbDevice.getChannel(channelNumber);

            bikeCadenceDisplay = new BikeCadenceDisplay(channelCad, network);
            bikeCadenceDisplay.TurnOn();
            bikeCadenceDisplay.SensorFound += (a, b) => Console.WriteLine("FE Sensor Found!");
            bikeCadenceDisplay.BikeCadenceDataReceived += (a, b) => Console.WriteLine("CAD Data : " + a.Cadence);
        }
    }
    
    class FECCommander
    {
        float transmittedGrade = 0.0f;
        long lastTransmittedGradeTime = 0l;
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



            gpxFile.Write(@"  </trkseg>
    </trk>
</gpx> ");
            gpxFile.Close();

            posFile.Close();


            mm.close();
        }

        private void initMmap()
        {
            var mmHrFile = open(Consts.BASE_OUT_PATH + @"\hr.bin", "rb");
            mmHr = mmap.mmap(mmHrFile.fileno(), 32, access = mmap.ACCESS_READ);
            mmHrFile.close();

            var mmCadFile = open(Consts.BASE_OUT_PATH + @"\cad.bin", "rb");
            mmCad = mmap.mmap(mmCadFile.fileno(), 32, access = mmap.ACCESS_READ);
            mmCadFile.close();

            var mmFile = open(Consts.BASE_OUT_PATH + @"\mm.bin", "rb+");
            mm = mmap.mmap(mmFile.fileno(), 32);
            mmFile.close();
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
            var posFile = new StreamWriter(String.Format(@"{0}\bkool-{1}.csv", Consts.BASE_OUT_PATH, title), false, System.Text.Encoding.UTF8, 1024);
            var gpxFile = new StreamWriter(String.Format(@"{0}\bkool-{1}.gpx", Consts.BASE_OUT_PATH, title), false, System.Text.Encoding.UTF8, 1024);

            gpxFile.Write(String.Format(@"<?xml version=""1.0"" encoding=""UTF-8""?>
    <gpx
    xmlns=""http://www.topografix.com/GPX/1/1""
    xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
    xsi:schemaLocation=""http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd http://www.garmin.com/xmlschemas/GpxExtensions/v3 http://www.garmin.com/xmlschemas/GpxExtensionsv3.xsd http://www.garmin.com/xmlschemas/TrackPointExtension/v1 http://www.garmin.com/xmlschemas/TrackPointExtensionv1.xsd http://www.garmin.com/xmlschemas/GpxExtensions/v3 http://www.garmin.com/xmlschemas/GpxExtensionsv3.xsd http://www.garmin.com/xmlschemas/TrackPointExtension/v1 http://www.garmin.com/xmlschemas/TrackPointExtensionv1.xsd""
    xmlns:gpxtpx=""http://www.garmin.com/xmlschemas/TrackPointExtension/v1""
    xmlns:gpxx=""http://www.garmin.com/xmlschemas/GpxExtensions/v3""
    creator=""AssettoStrada"" version=""1.1"">
    <trk>
    <name>{0}</name>
    <trkseg>\n", title));
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



            mmHr.seek(0);

            var readHr = mmHr.read(4);
            var readHrUtf8 = readHr.decode("utf-8");

            var hr = (int)readHrUtf8.split('|')[0];
            myLog(String.Format("HR {0}", hr));

            mmCad.seek(0);
            var readCad = mmCad.read(4);
            var readCadUtf8 = readCad.decode("utf-8");

            var cad = (int)readCadUtf8.split('|')[0];
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
            gpxFile.Write(String.Format(@"   <trkpt lon=""{0:.6f}"" lat=""{1:.6f}"" >
    <ele>{2:.2f}</ele>
    <time>{3:s}</time>
{4}
   </trkpt>\n",
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

        private void found()
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
            speedKmh = page.Speed * 0.0036f;

            pid.SetPoint = speedKmh;
        }

        private void on_page_specific_trainer(SpecificTrainerPage page, uint counter)
        {
            lastPower = page.InstantaneousPower;
            mm.seek(0);
            mm.write(String.Format("{0:06.2f}|{1:06.2f}|{2:06.2f}|",
                (float)page.InstantaneousPower,
                pid.SetPoint,
                transmittedGrade));
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


        private void send_basic_resistance()
        {
            var command = new ControlBasicResistancePage();
            command.TotalResistance = 80; // 40%
            simulator.SendBasicResistance(command);
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
