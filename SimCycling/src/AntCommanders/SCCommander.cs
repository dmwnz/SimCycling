using System;
using AntPlus.Profiles.Common;
using AntPlus.Profiles.BikeSpeedCadence;
using AntPlus.Profiles.BikeCadence;
using SimCycling.State;

namespace SimCycling
{
    class SCCommander : AbstractANTCommander
    {
        readonly BikeSpeedCadenceDisplay simulator;
        private float _lastCadence;
        public float LastCadence
        {
            get => IsLastValueOutdated ? 0.0f : _lastCadence;
            set { _lastCadence = value; LastMessageReceivedTime = DateTime.Now; }
        }

        public SCCommander(BikeSpeedCadenceDisplay simulator, UInt16 deviceNumber = 0)
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
            simulator.BikeCadenceDataReceived += OnCadencePage;
            simulator.TurnOn();
        }

        public void Stop()
        {
            simulator.SensorFound -= Found;
            simulator.BikeCadenceDataReceived -= OnCadencePage;
            simulator.TurnOff();
        }

        private void Found(ushort a, byte b)
        {
            Log("Speed/Cadence found ! ({0})", a);
            IsFound = true;
        }

        private void OnCadencePage(BikeCadenceData page, uint counter)
        {
            LastCadence = (float) page.Cadence;
        }
    }
}
