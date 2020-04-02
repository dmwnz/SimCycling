using System;
using System.Globalization;
using System.Threading;


namespace SimCycling
{
    class Launcher
    {
        static ANTDeviceManager manager;
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            //SetThreadUILanguage((ushort)Thread.CurrentThread.CurrentUICulture.LCID);

            manager = new ANTDeviceManager();

            manager.Start();

            Console.Read();
            manager.Stop();
        }
        static void OnProcessExit(object sender, EventArgs e)
        {
            
        }
    }
}
