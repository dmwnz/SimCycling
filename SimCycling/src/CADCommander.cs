using AntPlus.Profiles.BikeCadence;
using SimCycling.Utils;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace SimCycling.src
{
    class CADCommander
    {
        BikeCadenceDisplay simulator;
        MemoryMappedFile mm;

        public CADCommander(BikeCadenceDisplay simulator)
        {
            this.simulator = simulator;
            simulator.SensorFound += found;
            simulator.BikeCadenceDataReceived += Simulator_BikeCadenceDataReceived;
        }

        public void Start()
        {
            var hrFilePath = String.Format(@"{0}\cad.bin", Consts.BASE_OUT_PATH);
            mm = MemoryMappedFile.CreateFromFile(hrFilePath, FileMode.Open, "cad.bin");
        }

        private void found(ushort a, byte b)
        {
            Console.WriteLine("Cadence sensor Found !");
        }

        private void Simulator_BikeCadenceDataReceived(BikeCadenceData page, uint a)
        {
            var cad = page.Cadence;

            var mmAccessor = mm.CreateViewAccessor();
            var data = String.Format("{0}|", cad).ToCharArray();

            mmAccessor.WriteArray(0, data, 0, data.Length);
            mmAccessor.Dispose();
        }
    }
}
