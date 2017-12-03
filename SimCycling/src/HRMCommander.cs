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
            mm = MemoryMappedFile.CreateNew("hr.bin", 32);

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

            var mmAccessor = mm.CreateViewAccessor();
            var data = String.Format("{0}|", hr).ToCharArray();

            mmAccessor.WriteArray(0, data, 0, data.Length);
            mmAccessor.Dispose();
        }

    }
}
