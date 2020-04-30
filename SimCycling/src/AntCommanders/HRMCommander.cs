using AntPlus.Profiles.HeartRate;
using SimCycling.State;
using System;

namespace SimCycling
{
    class HRMCommander
    {
        readonly HeartRateDisplay simulator;
        public int LastBPM { get; set; }
        public bool IsFound { get; set; }

        public HRMCommander(HeartRateDisplay simulator, UInt16 deviceNumber = 0)
        {
            this.simulator = simulator;
            if (deviceNumber > 0)
            {
                this.simulator.ChannelParameters.DeviceNumber = deviceNumber;
            }
        }

        public void Start()
        {
            simulator.SensorFound += Found;
            simulator.HeartRateDataReceived += OnPageHeartRate;
            simulator.TurnOn();
        }

        public void Stop()
        {
            simulator.SensorFound -= Found;
            simulator.HeartRateDataReceived -= OnPageHeartRate;
            simulator.TurnOff();
        }

        private void Found(ushort a, byte b)
        {
            Console.WriteLine("HRM Found ! ({0})", a);
            IsFound = true;
        } 


        private void OnPageHeartRate(HeartRateData page, uint a)
        {
            LastBPM = page.HeartRate;
        }

    }
}
