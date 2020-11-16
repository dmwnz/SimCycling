using System;
using System.Configuration;
using System.Globalization;

namespace SimCycling
{
    abstract class AbstractANTCommander
    {
        private static readonly float SensorValueLifespan = float.Parse(ConfigurationManager.AppSettings["sensor_value_lifespan"], new CultureInfo("en-US", false).NumberFormat);

        public bool IsFound { get; set; }

        /// <summary>
        /// Last time we received a new message (different from the previous one)
        /// </summary>
        protected DateTime LastNewMessageReceivedTime { get; set; }

        /// <summary>
        /// Last time we received a message
        /// </summary>
        protected DateTime LastMessageReceivedTime { get; set; }

        protected bool IsLastValueOutdated => DateTime.Now > LastNewMessageReceivedTime.AddSeconds(SensorValueLifespan);
        protected bool NoMessageSinceALongTime => DateTime.Now > LastMessageReceivedTime.AddSeconds(SensorValueLifespan * 2);

        private ulong _lastUniqueEvent;
        protected ulong LastUniqueEvent
        {
            get { return _lastUniqueEvent; }
            set
            {
                LastMessageReceivedTime = DateTime.Now;
                if (value != _lastUniqueEvent)
                {
                    _lastUniqueEvent = value;
                    LastNewMessageReceivedTime = DateTime.Now;
                }
            }
        }

    }
}
