using System;
using AntPlus.Profiles.Common;
using AntPlus.Profiles.BikePower;
using SimCycling.State;

namespace SimCycling
{
    class BPCommander
    {

        public float LastPower { get; set; }
        public float LastCadence { get; set; }
        public bool IsFound { get; set; }


        readonly BikePowerDisplay simulator;

        public BPCommander(BikePowerDisplay simulator, UInt16 deviceNumber=0)
        {
            this.simulator = simulator;
            if (deviceNumber > 0)
            {
                this.simulator.ChannelParameters.DeviceNumber = deviceNumber;
            }
        }
        
        public static void Log(String s, params object[] parms)
        {
            Console.WriteLine(s, parms);
        }

        public void Start()
        {
            simulator.SensorFound += Found;

            simulator.StandardPowerOnlyPageReceived += OnPowerPage;
            simulator.TurnOn();
        }

        public void Stop()
        {
            simulator.SensorFound -= Found;
            simulator.StandardPowerOnlyPageReceived -= OnPowerPage;
            simulator.TurnOff();
        }

        private void Found(ushort a, byte b)
        {
            Log("Power found ! ({0})", a);
            IsFound = true;
            RequestCommandStatus();
        }

        private void OnPowerPage(StandardPowerOnlyPage page, uint counter)
        {
            LastPower = page.InstantaneousPower;
            LastCadence = page.InstantaneousCadence;
        }
        private void RequestCommandStatus()
        {
            var request = new RequestDataPage
            {
                RequestedPageNumber = 0x47 //  # Command Status page (0x47)
            };
            simulator.SendDataPageRequest(request);
        }
    }
}
