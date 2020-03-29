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

    
    abstract class GameInterface
    {
        List<Updateable> updateables;

        public abstract void Start();
        public abstract void Stop();

        void Update()
        {
            foreach (Updateable updateable in updateables)
            {
                updateable.Update();
            }
        }
    }
}
