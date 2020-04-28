using System;
using AntPlus.Profiles.Common;
using AntPlus.Profiles.BikePower;
using SimCycling.State;

namespace SimCycling
{
    class BPCommander
    {
        // bool acquiredVJoy = false;
        int lastPower = 0;

        readonly BikePowerDisplay simulator;

        public BPCommander(BikePowerDisplay simulator)
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
            Log("Power found !");
            RequestCommandStatus();
        }

        private void OnPowerPage(StandardPowerOnlyPage page, uint counter)
        {
            lastPower = page.InstantaneousPower;
            var cad = page.InstantaneousCadence;
            AntManagerState.Instance.CyclistPower = lastPower;
            AntManagerState.Instance.BikeCadence = (int)Math.Round((double) cad);
            AntManagerState.WriteToMemory();
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
