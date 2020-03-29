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
    class ACInterface
    {
        AssettoCorsa ac;
        PID pid;

        Point3D frontCoordinates = new Point3D(0, 0, 0);
        Point3D rearCoordinates = new Point3D(0, 0, 0);
        string track;
        bool isSpeedInit;

        List<Updateable> updateables;
        JoyControl joyControl;

        public ACInterface(List<Updateable> updateables, JoyControl joyControl)
        {
            this.updateables = updateables;
            this.joyControl = joyControl;
            Start();
        }

        public static void Log(String s, params object[] parms)
        {
            Console.WriteLine(s, parms);
        }

        public void Stop()
        {


            ac.PhysicsUpdated -= OnACPhysics;
            ac.GraphicsUpdated -= OnACGraphics;
            ac.StaticInfoUpdated -= OnACInfo;
            ac.Stop();
        }
     
        private void Start()
        {
            Log("Init Ac");
            ac = new AssettoCorsa
            {
                PhysicsInterval = 100,
                GraphicsInterval = 1000,
                StaticInfoInterval = 1000
            };

            ac.PhysicsUpdated += OnACPhysics;
            ac.GraphicsUpdated += OnACGraphics;
            ac.StaticInfoUpdated += OnACInfo;


            ac.Start();
            isSpeedInit = false;
            Log("AC Is running : " + ac.IsRunning);


            var P = 0.3f;
            var I = 0.005f;
            var D = 0.0f;

            pid = new PID(P, I, D);
            pid.Clear();
        }
        private void OnACInfo(object sender, StaticInfoEventArgs e)
        {
            track = e.StaticInfo.Track;
            Log("TRACK : " + track);
        }

        private void OnACGraphics(object sender, GraphicsEventArgs e)
        {
            Log("On AC Graphics");

            var altitudeDiff = frontCoordinates.Y - rearCoordinates.Y;
            var distance = Consts.CalcDistance(rearCoordinates, frontCoordinates);


            var newPitch = (float)Math.Round(altitudeDiff * 1000.0f / distance) / 10.0f;
            if (float.IsNaN(newPitch))
            {
                newPitch = 0;
            }
            AntManagerState.GetInstance().BikeIncline = newPitch;


            //WriteGPXLine();
            Log(String.Format("newPitch : {0}", newPitch));


            //if (equipmentFound)
            //{
            //    if (DateTimeOffset.Now.ToUnixTimeMilliseconds() > lastTransmittedGradeTime + 2)
            //    {
            //        Log(String.Format("sending new pitch {0}", newPitch));
            //        transmittedGrade = newPitch;
            //        SendTrackResistance(newPitch);
            //        lastTransmittedGradeTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            //    }
            //}
            foreach (Updateable updateable in updateables)
            {
                updateable.Update();
            }
        }
        private void OnACPhysics(object sender, PhysicsEventArgs e)
        {
            //#myLog("On Ac Physics")

            //#myLog("AC Speed : {0}".format(e.Physics.SpeedKmh))
            // airdensity = e.Physics.AirDensity;

            var frontX = (e.Physics.TyreContactPoint[0].X + e.Physics.TyreContactPoint[1].X) / 2.0;
            var frontY = (e.Physics.TyreContactPoint[0].Y + e.Physics.TyreContactPoint[1].Y) / 2.0;
            var frontZ = (e.Physics.TyreContactPoint[0].Z + e.Physics.TyreContactPoint[1].Z) / 2.0;

            var rearX = (e.Physics.TyreContactPoint[2].X + e.Physics.TyreContactPoint[3].X) / 2.0;
            var rearY = (e.Physics.TyreContactPoint[2].Y + e.Physics.TyreContactPoint[3].Y) / 2.0;
            var rearZ = (e.Physics.TyreContactPoint[2].Z + e.Physics.TyreContactPoint[3].Z) / 2.0;


            frontCoordinates = new Point3D(frontX, frontY, frontZ);
            rearCoordinates = new Point3D(rearX, rearY, rearZ);
            if (!isSpeedInit)
            {
                AntManagerState.GetInstance().BikeSpeedKmh = e.Physics.SpeedKmh;
                isSpeedInit = true;
            }
           



            pid.SetPoint = AntManagerState.GetInstance().BikeSpeedKmh;

            var acSpeed = e.Physics.SpeedKmh;
            pid.Update(acSpeed);
            var coeff = pid.Output;
            joyControl.Throttle(coeff);

        }
    }
}
