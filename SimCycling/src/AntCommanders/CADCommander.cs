using AntPlus.Profiles.BikeCadence;
using SimCycling.State;
using System;

namespace SimCycling
{
    class CADCommander
    {
        readonly BikeCadenceDisplay simulator;
        public float LastCadence { get; set; }
        public bool IsFound { get; set; }

        public CADCommander(BikeCadenceDisplay simulator, UInt16 deviceNumber = 0)
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
            simulator.BikeCadenceDataReceived += OnPageBikeCadence;
            simulator.TurnOn();
        }

        public void Stop()
        {
            simulator.SensorFound -= Found;
            simulator.BikeCadenceDataReceived -= OnPageBikeCadence;
            simulator.TurnOff();
        }

        private void Found(ushort a, byte b)
        {
            Console.WriteLine("Cadence sensor Found ! (%i)", a);
            IsFound = true;
        }

        private void OnPageBikeCadence(BikeCadenceData page, uint a)
        {
            LastCadence = (float) page.Cadence;
        }
    }
}
