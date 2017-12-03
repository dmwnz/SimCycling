using System;
using ANT_Managed_Library;
using AntPlus.Profiles.HeartRate;
using AntPlus.Profiles.FitnessEquipment;
using AntPlus.Profiles.BikeCadence;

namespace SimCycling
{
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
            var commander = new FECCommander(fitnessEquipmentDisplay);
            commander.Start();
            fitnessEquipmentDisplay.TurnOn();

            Console.Read();
            fitnessEquipmentDisplay.TurnOff();

            commander.Finish();

            //fitnessEquipmentDisplay.SensorFound += (a, b) => Console.WriteLine("FE Sensor Found!");
            //fitnessEquipmentDisplay.GeneralFePageReceived += (a, b) => Console.WriteLine("FE Data : " + a.Speed);
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
}
