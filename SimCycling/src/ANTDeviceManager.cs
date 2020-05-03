using System;
using ANT_Managed_Library;
using AntPlus.Profiles.HeartRate;
using AntPlus.Profiles.FitnessEquipment;
using AntPlus.Profiles.BikeCadence;
using AntPlus.Profiles.BikeSpeedCadence;
using AntPlus.Profiles.BikePower;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using SimCycling.Workout;
using SimCycling.State;

namespace SimCycling
{
    enum BikeModel : ushort
    {
        FEC = 0,
        BikePhysics = 1,
    }

    class ANTDeviceManager : Updateable
    {
        static readonly byte[] NETWORK_KEY = { 0xB9, 0xA5, 0x21, 0xFB, 0xBD, 0x72, 0xC3, 0x45 }; // ANT+ Managed network key
        static readonly byte CHANNEL_FREQUENCY = 0x39;

        ANT_Device usbDevice;

        HRMCommander hrmCommander;
        FECCommander fecCommander;
        CADCommander cadCommander;
        BPCommander bpCommander;
        SCCommander scCommander;

        ACInterface acInterface;
        System.DateTime previousFrameTimestamp = System.DateTime.Now;

        AntPlus.Types.Network network;
        BikeModel bikeModel = BikeModel.BikePhysics;
        GenericWorkout workout;

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
            Console.WriteLine(ConfigurationManager.AppSettings["model"]);
            if (ConfigurationManager.AppSettings["ridermodel"] == "fec")
            {
                Console.WriteLine("Using FEC for physical model.");
                bikeModel = BikeModel.FEC;
            }
            else if (ConfigurationManager.AppSettings["ridermodel"] == "physics")
            {
                Console.WriteLine("Using integrated model for physical model.");
                bikeModel = BikeModel.BikePhysics;
            }
            
            AntManagerState.Instance.CriticalPower = Single.Parse(ConfigurationManager.AppSettings["cp"]);

            InitHRM(0);
            InitCAD(1);
            InitFEC(2);
            InitBP(3);
            InitSC(4);
            InitAC();
            InitFIT();
        }

        internal void StopWorkout()
        {
            workout = null;
            AntManagerState.Instance.WorkoutName = null;
            AntManagerState.Instance.TargetPower = 0;
            AntManagerState.Instance.NextTargetPower = 0;
            AntManagerState.Instance.RemainingIntervalTime = 0;
            AntManagerState.Instance.RemainingTotalTime = 0;
            AntManagerState.Instance.WorkoutElapsedTime = 0;
            AntManagerState.Instance.WorkoutMessage = null;
            AntManagerState.WriteToMemory();

            FITRecorder.TerminateLap();
        }

        internal void StartWorkout(string filename)
        {
            try
            {
                if (bikeModel == BikeModel.FEC)
                {
                    throw new Exception("Workouts not compatible with FEC bike model");
                }
                AntManagerState.Instance.WorkoutName = filename;
                AntManagerState.Instance.WorkoutElapsedTime = 0;

                workout = GenericWorkout.Factory(AntManagerState.Instance.WorkoutName);
                FITRecorder.TerminateLap();
            }
            catch (Exception e)
            {
                workout = null;
                AntManagerState.Instance.WorkoutName = null;

                Console.WriteLine("Did not load workout." + e.Message);
            }
        }

        public void Stop()
        {
            StopWorkout();
            acInterface.Stop();
            FITRecorder.Stop();
            hrmCommander.Stop();
            cadCommander.Stop();
            fecCommander.Stop();
            bpCommander.Stop();            
        }

        void InitHRM(int channelNumber)
        {
            Int32 deviceNumber = Int32.Parse(ConfigurationManager.AppSettings["hrm_device"]);
            if (deviceNumber < 0)
            {
                return;
            }
            var channelHrm = usbDevice.getChannel(channelNumber);
            var heartRateDisplay = new HeartRateDisplay(channelHrm, network);
            hrmCommander = new HRMCommander(heartRateDisplay, (UInt16) deviceNumber) ;
            hrmCommander.Start();
        }

        void InitFEC(int channelNumber)
        {
            Int32 deviceNumber = Int32.Parse(ConfigurationManager.AppSettings["fec_device"]);
            if (deviceNumber < 0)
            {
                return;
            }
            var channelFec = usbDevice.getChannel(channelNumber);
            var fitnessEquipmentDisplay = new FitnessEquipmentDisplay(channelFec, network);
            var useAsModel = false;
            fecCommander = new FECCommander(fitnessEquipmentDisplay, (UInt16) deviceNumber);
            fecCommander.Start();
        }

        void InitCAD(int channelNumber)
        {
            Int32 deviceNumber = Int32.Parse(ConfigurationManager.AppSettings["cadence_device"]);
            if (deviceNumber < 0)
            {
                return;
            }
            var channelCad = usbDevice.getChannel(channelNumber);
            var bikeCadenceDisplay = new BikeCadenceDisplay(channelCad, network);
            cadCommander = new CADCommander(bikeCadenceDisplay, (UInt16) deviceNumber);
            cadCommander.Start();
        }

        void InitBP(int channelNumber)
        {
            Int32 deviceNumber = Int32.Parse(ConfigurationManager.AppSettings["bike_power_device"]);
            if (deviceNumber < 0)
            {
                return;
            }
            AntManagerState.Instance.TripTotalKm = 0;
            var channelCad = usbDevice.getChannel(channelNumber);
            var bikePowerDisplay = new BikePowerDisplay(channelCad, network);
            bpCommander = new BPCommander(bikePowerDisplay, (UInt16) deviceNumber);
            bpCommander.Start();
        }

        void InitSC(int channelNumber)
        {
            Int32 deviceNumber = Int32.Parse(ConfigurationManager.AppSettings["speed_cadence_device"]);
            if (deviceNumber < 0)
            {
                return;
            }
            AntManagerState.Instance.TripTotalKm = 0;
            var channelCad = usbDevice.getChannel(channelNumber);
            var speedCadenceDisplay = new BikeSpeedCadenceDisplay(channelCad, network);
            scCommander = new SCCommander(speedCadenceDisplay, (UInt16) deviceNumber);
            scCommander.Start();
        }

        void InitAC()
        {
            List<Updateable> updateables = new List<Updateable>();
            updateables.Add(fecCommander);
            updateables.Add(this);

            if (bikeModel == BikeModel.BikePhysics)
            {
                CultureInfo cul = new CultureInfo("en-US", false);
                var CdA = float.Parse(ConfigurationManager.AppSettings["cda"], cul.NumberFormat);
                var Cxx = float.Parse(ConfigurationManager.AppSettings["cxx"], cul.NumberFormat);
                var bikeweight = float.Parse(ConfigurationManager.AppSettings["bikeweight"], cul.NumberFormat);
                var riderweight = float.Parse(ConfigurationManager.AppSettings["riderweight"], cul.NumberFormat);
                var bikePhysics = new BikePhysics(CdA, Cxx, bikeweight + riderweight);
                updateables.Add(bikePhysics);
            }

            var joyControl = new JoyControl();
            acInterface = new ACInterface(updateables, joyControl);
            acInterface.NewLap += OnNewLap;
            bpCommander.Start();
        }


        public void OnNewLap()
        {
            if(workout == null)
            {
                FITRecorder.TerminateLap();
            }
        }

        override public void Update()
        {
            var time = System.DateTime.Now;
            double dt = time.Subtract(previousFrameTimestamp).TotalSeconds;
            previousFrameTimestamp = time;

            FITRecorder.AddRecord();
            AntManagerState.Instance.TripTotalKm += (float)(AntManagerState.Instance.BikeSpeedKmh / 1000 / 3.6 * dt);
            AntManagerState.Instance.TripTotalTime += (float)dt;

            // Take power from PM first, then smart trainer if unavailable
            if (bpCommander.IsFound)
            {
                AntManagerState.Instance.CyclistPower = bpCommander.LastPower;
            }
            else if (fecCommander.IsFound)
            {
                AntManagerState.Instance.CyclistPower = bpCommander.LastPower;
            }
            else
            {
                AntManagerState.Instance.CyclistPower = 0;
            }

            // Take cadence from cadence, then speed/cadence, then PM
            if (cadCommander.IsFound)
            {
                AntManagerState.Instance.BikeCadence = (int) Math.Round(cadCommander.LastCadence);
            }
            else if (scCommander.IsFound)
            {
                AntManagerState.Instance.BikeCadence = (int)Math.Round(scCommander.LastCadence);
            }
            else if (bpCommander.IsFound)
            {
                AntManagerState.Instance.BikeCadence = (int)Math.Round(bpCommander.LastCadence);
            }
            else
            {
                AntManagerState.Instance.BikeCadence = 0;
            }

            if (hrmCommander.IsFound)
            {
                AntManagerState.Instance.CyclistHeartRate = hrmCommander.LastBPM;
            }
            else
            {
                AntManagerState.Instance.CyclistHeartRate = 0;
            }

            if (fecCommander.IsFound && bikeModel == BikeModel.FEC)
            {
                AntManagerState.Instance.BikeSpeedKmh = fecCommander.SpeedKmh;
            }

            AntManagerState.WriteToMemory();

            Console.WriteLine("update");

            workout?.Update();
            if (workout?.IsFinished == false)
            {
                AntManagerState.Instance.WorkoutElapsedTime += (float)dt;
            }
            else if (workout?.IsFinished == true)
            {
                StopWorkout();
            }
        }

        void InitFIT()
        {
            FITRecorder.Start();
        }
    }
}
