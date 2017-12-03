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
            mm = MemoryMappedFile.CreateOrOpen("cad.bin", 32);

            using (var mmAccessor = mm.CreateViewAccessor())
            {
                mmAccessor.WriteArray(0, new char[] { '0', '|' }, 0, 2);
            }

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
            var data = String.Format("{0}|", cad).ToCharArray();

            using (var mmAccessor = mm.CreateViewAccessor())
            {
                mmAccessor.WriteArray(0, data, 0, data.Length);
            }
        }
    }
}
