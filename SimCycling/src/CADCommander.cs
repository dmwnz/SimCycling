using AntPlus.Profiles.BikeCadence;
using SimCycling.State;
using System;

namespace SimCycling
{
    class CADCommander
    {
        readonly BikeCadenceDisplay simulator;

        public CADCommander(BikeCadenceDisplay simulator)
        {
            this.simulator = simulator;
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
            Console.WriteLine("Cadence sensor Found !");
        }

        private void OnPageBikeCadence(BikeCadenceData page, uint a)
        {
            var cad = page.Cadence;
            Console.WriteLine("CAD : " + cad);
            AntManagerState.Instance.BikeCadence = (int)Math.Round(cad);
            AntManagerState.WriteToMemory();
        }
    }
}
