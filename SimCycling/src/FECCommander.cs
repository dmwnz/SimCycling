using System;
using System.Text;
using SimCycling.Utils;

using System.IO;
using AntPlus.Profiles.Common;
using System.Globalization;
using AntPlus.Profiles.FitnessEquipment;
using System.Configuration;

namespace SimCycling
{
    class FECCommander : Updateable
    {
        DateTime lastTransmittedGradeTime;
        
        readonly bool useAsModel;


        float speedKmh;

        readonly FitnessEquipmentDisplay simulator;

        public FECCommander(FitnessEquipmentDisplay simulator, bool useAsModel=true)
        {
            this.simulator = simulator;
            this.useAsModel = useAsModel;
        }
        
        public static void Log(String s, params object[] parms)
        {
            Console.WriteLine(s, parms);
        }

        public void Start()
        {
            simulator.SensorFound += Found;

            simulator.CommandStatusPageReceived += OnPageCommandStatus;
            simulator.FeCapabilitiesPageReceived += OnPageFeCapabilities;
            simulator.GeneralFePageReceived += OnPageGeneralFE;
            simulator.SpecificTrainerPageReceived += OnPageSpecificTrainer;

            simulator.TurnOn();
            lastTransmittedGradeTime = DateTime.Now;

        }

        public void Stop()
        {
            simulator.SensorFound -= Found;

            simulator.CommandStatusPageReceived -= OnPageCommandStatus;
            simulator.FeCapabilitiesPageReceived -= OnPageFeCapabilities;
            simulator.GeneralFePageReceived -= OnPageGeneralFE;
            simulator.SpecificTrainerPageReceived -= OnPageSpecificTrainer;

            simulator.TurnOff();
        }

        private void Found(ushort a, byte b)
        {
            Log("Bkool found !");

            RequestCommandStatus();
        }

        private void OnPageGeneralFE(GeneralFePage page, uint counter)
        {            
            if (useAsModel)
            {
                speedKmh = page.Speed * 0.0036f;
                AntManagerState.GetInstance().BikeSpeedKmh = speedKmh;
                AntManagerState.WriteToMemory();
            }
        }

        private void OnPageSpecificTrainer(SpecificTrainerPage page, uint counter)
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            AntManagerState.GetInstance().CyclistPower = page.InstantaneousPower;
            AntManagerState.WriteToMemory();
            
        }

        private void OnPageFeCapabilities(FeCapabilitiesPage page, uint counter)
        {
            Log("Basic resistance " +
                page.FeCapabilities.BasicResistanceModeSupported);
            Log("Power " +
                page.FeCapabilities.TargetPowerModeSupported);
            Log("Simulation " +
                page.FeCapabilities.SimulationModeSupported);
        }

        private void OnPageCommandStatus(CommandStatusPage page, uint counter)
        {
            Log("status : " + page.LastReceivedCommandID);
            SendUserConfiguration();
        }

        private void SendUserConfiguration()
        {
            CultureInfo cul = new CultureInfo("en-US", false);
            var bikeWeight = float.Parse(ConfigurationManager.AppSettings["bikeweight"], cul.NumberFormat);
            var riderWeight = float.Parse(ConfigurationManager.AppSettings["riderweight"], cul.NumberFormat);

            // 170//8.5kg
            // 6250//62.5kg
            var command = new UserConfigurationPage
            {
                BikeWeight = (ushort)(bikeWeight*20), 
                UserWeight = (ushort)(riderWeight*100), 
                WheelDiameter = 62
            };
            simulator.SendUserConfiguration(command);
        }


        private void SendTargetPower(float power)
        {
            var command = new ControlTargetPowerPage
            {
                TargetPower = (ushort)(power * 4)
            };
            simulator.SendTargetPower(command);
        }

        private void SendWindResistance()
        {
            var command = new ControlWindResistancePage
            {
                // Product of Frontal Surface Area, Drag Coefficient and Air Density.
                // Use default value: 0xFF
                // Unit 0.01 kg/m
                // Range 0.00 – 1.86kg/m
                WindResistanceCoefficient = 0xFF,
                // Speed of simulated wind acting on the cyclist.(+) –Head Wind(–) –Tail Wind
                // Use default value: 0xFF
                // Unit km/h
                // Range -127 – +127 km/h
                WindSpeed = 0xFF,
                // Simulated drafting scale factor
                // Use default value: 0xFF
                // Unit 0.01
                // Range 0 – 1.00
                DraftingFactor = 0xFF //# 0.1
            };
            simulator.SendWindResistance(command);
        }

        private void SendTrackResistance(float grade)
        {
            var gradeToTransmit = Consts.ConvertGrade(grade);
            var command = new ControlTrackResistancePage
            {
                Grade = gradeToTransmit
            };
            simulator.SendTrackResistance(command);
        }

        private void RequestCommandStatus()
        {
            var request = new RequestDataPage
            {
                RequestedPageNumber = 0x47 //  # Command Status page (0x47)
            };
            simulator.SendDataPageRequest(request);
        }

        private void RequestFECapabilities()
        {
            var request = new RequestDataPage
            {
                RequestedPageNumber = 0x36  //# FE Capabilities page (0x36)
            };
            simulator.SendDataPageRequest(request);
        }

        public override void Update()
        {
            var targetPower = AntManagerState.GetInstance().TargetPower;
            var t = DateTime.Now;

            if (targetPower > 0)
            {
                SendTargetPower(targetPower);
            }
            else if (t.Subtract(lastTransmittedGradeTime).TotalSeconds > 2)
            {
                SendWindResistance();
                SendTrackResistance(AntManagerState.GetInstance().BikeIncline);
                lastTransmittedGradeTime = t;
            }

        }
    }
}
