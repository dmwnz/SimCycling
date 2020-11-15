using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimCycling
{
    abstract class AbstractANTCommander
    {
        private static readonly float SensorValueLifespan = float.Parse(ConfigurationManager.AppSettings["sensor_value_lifespan"], new CultureInfo("en-US", false).NumberFormat);

        public bool IsFound { get; set; }

        public DateTime LastMessageReceivedTime { get; set; }

        protected bool IsLastValueOutdated => DateTime.Now > LastMessageReceivedTime.AddSeconds(SensorValueLifespan);
    }
}
