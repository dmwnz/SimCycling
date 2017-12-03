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
            simulator.SensorFound += found;
            simulator.HeartRateDataReceived += on_page_heart_rate;
        }

        public void Start()
        {
            var hrFilePath = String.Format(@"{0}\hr.bin", Consts.BASE_OUT_PATH);
            mm = MemoryMappedFile.CreateFromFile(hrFilePath, FileMode.Open, "hr.bin");
        }

        private void found(ushort a, byte b)
        {
            Console.WriteLine("HRM Found !");
        } 


        private void on_page_heart_rate(HeartRateData page, uint a)
        {
            var hr = page.HeartRate;

            var mmAccessor = mm.CreateViewAccessor();
            var data = String.Format("{0}|", hr).ToCharArray();

            mmAccessor.WriteArray(0, data, 0, data.Length);
            mmAccessor.Dispose();
        }

    }
}
