using System;
using ANT_Managed_Library;
using AntPlus.Profiles.HeartRate;
using AntPlus.Profiles.FitnessEquipment;
using AntPlus.Profiles.BikeCadence;
using System.IO.MemoryMappedFiles;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Runtime.Serialization;
using System.Linq;

namespace SimCycling
{
    [DataContract]
    class AntManagerState
    {
        // Singleton instance
        private static readonly AntManagerState antManagerState = new AntManagerState();
        private static MemoryMappedFile mm;

        [DataMember()]
        public int BikeCadence { get; set; }

        [DataMember()]
        public float BikeSpeedKmh { get; set; }

        [DataMember()]
        public float BikeIncline { get; set; }

        [DataMember()]
        public int CyclistHeartRate { get; set; }

        [DataMember()]
        public float CyclistPower { get; set; }

        [DataMember()]
        public float TripTotalKm { get; set; }

        public static AntManagerState GetInstance()
        {
            return antManagerState;
        }

        public static void WriteToMemory()
        {
            mm = MemoryMappedFile.CreateOrOpen("SimCycling", 256);

            using (var mmAccessor = mm.CreateViewAccessor())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AntManagerState));
                byte[] array = Enumerable.Repeat((byte)0x0, 256).ToArray();
                MemoryStream ms = new MemoryStream(array);
                serializer.WriteObject(ms, GetInstance());
                mmAccessor.WriteArray(0, array, 0, 256);
            }
        }
    }

    class ANTDeviceManager
    {
        static readonly byte[] NETWORK_KEY = { 0xB9, 0xA5, 0x21, 0xFB, 0xBD, 0x72, 0xC3, 0x45 }; // ANT+ Managed network key
        static readonly byte CHANNEL_FREQUENCY = 0x39;

        ANT_Device usbDevice;

        HRMCommander hrmCommander;
        FECCommander fecCommander;
        CADCommander cadCommander;

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
            InitCAD(1);
            InitFEC(2);
        }

        public void Stop()
        {
            hrmCommander.Stop();
            cadCommander.Stop();
            fecCommander.Stop();
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
            fecCommander = new FECCommander(fitnessEquipmentDisplay);
            fecCommander.Start();
        }

        void InitCAD(int channelNumber)
        {
            var channelCad = usbDevice.getChannel(channelNumber);
            var bikeCadenceDisplay = new BikeCadenceDisplay(channelCad, network);
            cadCommander = new CADCommander(bikeCadenceDisplay);
            cadCommander.Start();
        }
    }
}
