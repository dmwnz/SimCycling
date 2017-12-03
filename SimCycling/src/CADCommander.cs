using AntPlus.Profiles.BikeCadence;
using SimCycling.Utils;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace SimCycling
{
    class CADCommander
    {
        BikeCadenceDisplay simulator;
        MemoryMappedFile mm;

        public CADCommander(BikeCadenceDisplay simulator)
        {
            this.simulator = simulator;
        }

        public void Start()
        {
            //var hrFilePath = String.Format(@"{0}\cad.bin", Consts.BASE_OUT_PATH);
            mm = MemoryMappedFile.CreateNew("cad.bin", 32);

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

            var mmAccessor = mm.CreateViewAccessor();
            var data = String.Format("{0}|", cad).ToCharArray();

            mmAccessor.WriteArray(0, data, 0, data.Length);
            mmAccessor.Dispose();
        }
    }
}
