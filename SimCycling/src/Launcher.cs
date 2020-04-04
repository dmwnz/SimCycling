using System;
using System.Globalization;
using System.Threading;


namespace SimCycling
{

    
    class Launcher
    {
        static ANTDeviceManager manager;
        public static bool KeepRunning { get; set; } = true;
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e) {
                e.Cancel = true;
                KeepRunning = false;
            };

            manager = new ANTDeviceManager();

            manager.Start();
            ConsoleKeyInfo cki;

            while (KeepRunning)
            {
                Thread.Sleep(500);
                cki = Console.ReadKey(true);

                // Announce the name of the key that was pressed .
                Console.WriteLine($"  Key pressed: {cki.Key}\n");

                // Exit if the user pressed the 'X' key.
                if (cki.Key == ConsoleKey.X) break;
            }
           

            manager.Stop();
            Console.WriteLine("exited gracefully");
        }
        static void OnProcessExit(object sender, EventArgs e)
        {
            manager.Stop();
        }
    }
}
