using System;
using ANT_Managed_Library;
using AntPlus.Profiles.HeartRate;
using AntPlus.Profiles.FitnessEquipment;
using AntPlus.Profiles.BikeCadence;
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
            
            AntManagerState.GetInstance().CriticalPower = Single.Parse(ConfigurationManager.AppSettings["cp"]);
            InitHRM(0);
            InitCAD(1);
            InitFEC(2);
            InitBP(3);
            InitAC();
            InitFIT();
        }

        internal void StopWorkout()
        {
            workout = null;
            AntManagerState.GetInstance().WorkoutName = null;
            AntManagerState.GetInstance().TargetPower = 0;
            AntManagerState.GetInstance().NextTargetPower = 0;
            AntManagerState.GetInstance().RemainingIntervalTime = 0;
            AntManagerState.GetInstance().RemainingTotalTime = 0;
            AntManagerState.GetInstance().WorkoutElapsedTime = 0;
            AntManagerState.WriteToMemory();
        }

        internal void SetWorkout(string filename)
        {
            try
            {
                if (bikeModel == BikeModel.FEC)
                {
                    throw new Exception("Workouts not compatible with FEC bike model");
                }
                AntManagerState.GetInstance().WorkoutName = filename;
                AntManagerState.GetInstance().WorkoutElapsedTime = 0;

                workout = GenericWorkout.Factory(AntManagerState.GetInstance().WorkoutName);
            }
            catch (Exception e)
            {
                workout = null;
                AntManagerState.GetInstance().WorkoutName = null;

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
            var channelHrm = usbDevice.getChannel(channelNumber);
            var heartRateDisplay = new HeartRateDisplay(channelHrm, network);
            hrmCommander = new HRMCommander(heartRateDisplay);
            hrmCommander.Start();
        }

        void InitFEC(int channelNumber)
        {
            var channelFec = usbDevice.getChannel(channelNumber);
            var fitnessEquipmentDisplay = new FitnessEquipmentDisplay(channelFec, network);
            var useAsModel = false;
            if (bikeModel == BikeModel.FEC)
            {
                useAsModel = true;
            }
            fecCommander = new FECCommander(fitnessEquipmentDisplay, useAsModel);
            fecCommander.Start();
        }

        void InitCAD(int channelNumber)
        {
            var channelCad = usbDevice.getChannel(channelNumber);
            var bikeCadenceDisplay = new BikeCadenceDisplay(channelCad, network);
            cadCommander = new CADCommander(bikeCadenceDisplay);
            cadCommander.Start();
        }

        void InitBP(int channelNumber)
        {
            AntManagerState.GetInstance().TripTotalKm = 0;
            var channelCad = usbDevice.getChannel(channelNumber);
            var bikePowerDisplay = new BikePowerDisplay(channelCad, network);
            bpCommander = new BPCommander(bikePowerDisplay);
            bpCommander.Start();
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


        public void OnNewLap(object sender, int lap)
        {
            if(workout != null)
            {

            }
            else
            {
                FITRecorder.Lap();
            }
        }

        override public void Update()
        {
            var time = System.DateTime.Now;
            double dt = time.Subtract(previousFrameTimestamp).TotalSeconds;
            previousFrameTimestamp = time;

            FITRecorder.AddRecord();
            AntManagerState.GetInstance().TripTotalKm += (float)(AntManagerState.GetInstance().BikeSpeedKmh / 1000 / 3.6 * dt);
            AntManagerState.GetInstance().TripTotalTime += (float)dt;

            Console.WriteLine("update");

            if (workout != null)
            {
                workout.Update();
                if (!workout.IsFinished)
                {
                    AntManagerState.GetInstance().WorkoutElapsedTime += (float)dt;
                }
                else
                {
                    workout = null;
                }
            }
            else
            {
                AntManagerState.GetInstance().WorkoutElapsedTime = 0;
            }
        }

        void InitFIT()
        {
            FITRecorder.Start();
        }
    }
}
