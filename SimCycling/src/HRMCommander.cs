using AntPlus.Profiles.HeartRate;
using SimCycling.State;
using System;

namespace SimCycling
{
    class HRMCommander
    {
        readonly HeartRateDisplay simulator;

        public HRMCommander(HeartRateDisplay simulator)
        {
            this.simulator = simulator;
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
            Console.WriteLine("HRM Found !");
        } 


        private void OnPageHeartRate(HeartRateData page, uint a)
        {
            var hr = page.HeartRate;
            Console.WriteLine("HRM : " + hr);
            AntManagerState.GetInstance().CyclistHeartRate = hr;
            AntManagerState.WriteToMemory();
        }

    }
}
