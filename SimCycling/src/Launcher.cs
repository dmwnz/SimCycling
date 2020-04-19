using System;
using System.Diagnostics;
using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SimCycling
{

    enum Signal : byte
    {
        NONE = 0,
        LOAD_WORKOUT = 1,
        STOP_WORKOUT = 2,
        EXIT = 3,
    }
    
    class Launcher
    {
        static ANTDeviceManager manager;
        public static bool KeepRunning { get; set; } = true;
        
        [STAThread]
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

                ReadSignal();
            }
           
            manager.Stop();
            Console.WriteLine("exited gracefully");
        }

        private static void ReadSignal()
        {
            var mm = MemoryMappedFile.CreateOrOpen("SimCyclingSignal", 1);
            Signal readSignal = Signal.NONE;
            using (var mmAccessor = mm.CreateViewAccessor())
            {
                readSignal = (Signal)mmAccessor.ReadByte(0);
                mmAccessor.Write(0, (byte)Signal.NONE);
            }
            switch (readSignal)
            {
                case Signal.LOAD_WORKOUT:
                    string filename = LoadWorkoutFile();
                    if (filename != null)
                    {
                        manager.SetWorkout(filename);
                    }
                    break;
                case Signal.STOP_WORKOUT:
                    manager.StopWorkout();
                    break;
                case Signal.EXIT:
                    KeepRunning = false;
                    break;
            }
        }


        [DllImport("user32.dll")]
        public static extern int SetForegroundWindow(int hwnd);
        
        private static string LoadWorkoutFile()
        {
            // put the console app window in focus , so when the file open dialog opens on top of it, it is in focus
            SetForegroundWindow(Process.GetCurrentProcess().MainWindowHandle.ToInt32());

            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\..";
                openFileDialog.Filter = "Zwift Workout files (*.zwo)|*.zwo|ERG workout files (*.erg)|*.erg|MRC workout files (*.mrc)|*.mrc";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    //Get the path of specified file
                    return openFileDialog.FileName;
                }
            }
            return null;
        }

        static void OnProcessExit(object sender, EventArgs e)
        {
            manager.Stop();
        }
    }
}
