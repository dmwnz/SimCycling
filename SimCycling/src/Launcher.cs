using System;


namespace SimCycling
{
    class Launcher
    {
        static void Main(string[] args)
        {
            var manager = new ANTDeviceManager();

            manager.Start();

            Console.Read();

            manager.Stop();
        }
    }
}
