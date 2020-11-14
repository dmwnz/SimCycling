using AntPlus.Profiles.BikeCadence;
using SimCycling.State;
using System;

namespace SimCycling
{
    class CADCommander : AbstractANTCommander
    {
        readonly BikeCadenceDisplay simulator;
        private float _lastCadence;
        public float LastCadence
        {
            get => IsLastValueOutdated ? 0.0f : _lastCadence;
            set { _lastCadence = value; LastMessageReceivedTime = DateTime.Now; }
        }

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
