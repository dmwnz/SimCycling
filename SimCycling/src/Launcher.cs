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
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            Console.Read();
        }
        static void OnProcessExit(object sender, EventArgs e)
        {
            manager.Stop();
        }
    }
}
