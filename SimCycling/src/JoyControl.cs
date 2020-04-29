using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Configuration;

using SimCycling.Utils;
using AssettoCorsaSharedMemory;
using vJoyInterfaceWrap;

namespace SimCycling
{

    
    class JoyControl
    {
        uint idVJoy;
        vJoy joystick;
        bool acquired;

        public JoyControl()
        {
            Start();
        }

        public void Start()
        {

            idVJoy = 1u;
            joystick = new vJoy();

            var enabled = joystick.vJoyEnabled();
            if (!enabled)
            {
                Console.WriteLine("vJoy driver not enabled: Failed Getting vJoy attributes.");
                return;
            }

            var vjdStatus = joystick.GetVJDStatus(idVJoy);
            Console.WriteLine(vjdStatus.ToString());
            switch (vjdStatus)
            {
                case VjdStat.VJD_STAT_OWN:
                    Console.WriteLine("vJoy Device {0} is already owned by this feeder\n", idVJoy);
                    break;
                case VjdStat.VJD_STAT_FREE:
                    Console.WriteLine("vJoy Device {0} is free\n", idVJoy);
                    break;
                case VjdStat.VJD_STAT_BUSY:
                    Console.WriteLine("vJoy Device {0} is already owned by another feeder\nCannot continue\n", idVJoy);
                    return;
                case VjdStat.VJD_STAT_MISS:
                    Console.WriteLine("vJoy Device {0} is not installed or disabled\nCannot continue\n", idVJoy);
                    return;
                default:
                    Console.WriteLine("vJoy Device {0} general error\nCannot continue\n", idVJoy);
                    return;
            }


            acquired = joystick.AcquireVJD(idVJoy);
            if (!acquired)
            {
                Console.WriteLine("Failed to acquire vJoy device number {0}.\n", idVJoy);
                return;
            }
            Console.WriteLine("Acquired: vJoy device number {0}.\n", idVJoy);

        }
        public void Stop()
        {
            if (acquired)
            {
                joystick.RelinquishVJD(idVJoy);
            }
        }

        public void Throttle(float value)
        {
            if (acquired)
            {
                var axisVal = (int)Math.Round(value * 16384) + 16383;
                //# myLog("SetAxis " + str(axisVal));
                joystick.SetAxis(axisVal, idVJoy, HID_USAGES.HID_USAGE_X);
            }
        }

        public void Direction(float value)
        {
            if (acquired)
            {
                var axisVal = (int)Math.Round(value * 16384) + 16383;
                //# myLog("SetAxis " + str(axisVal));
                joystick.SetAxis(axisVal, idVJoy, HID_USAGES.HID_USAGE_Z);
            }
        }
    }
}
