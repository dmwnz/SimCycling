using System;


namespace SimCycling
{
    class Launcher
    {
        static void Main(string[] args)
        {
            //ANTDeviceManager manager = new ANTDeviceManager();
            //manager.Start();


            var manager = new ANTDeviceManager();

            manager.Start();

            Console.Read();

        }
    }
}
