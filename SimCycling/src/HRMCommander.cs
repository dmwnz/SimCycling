using AntPlus.Profiles.HeartRate;
using SimCycling.Utils;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace SimCycling
{
    class HRMCommander
    {
        HeartRateDisplay simulator;
        MemoryMappedFile mm;

        public HRMCommander(HeartRateDisplay simulator)
        {
            this.simulator = simulator;
        }

        public void Start()
        {
            //var hrFilePath = String.Format(@"{0}\hr.bin", Consts.BASE_OUT_PATH);
            mm = MemoryMappedFile.CreateOrOpen("hr.bin", 32);

            using (var mmAccessor = mm.CreateViewAccessor())
            {
                mmAccessor.WriteArray(0, new char[]{ '0', '|' }, 0, 2);
            }

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
            var data = String.Format("{0}|", hr).ToCharArray();

            using (var mmAccessor = mm.CreateViewAccessor())
            {
                mmAccessor.WriteArray(0, data, 0, data.Length);
            }
        }

    }
}
