using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace SimCycling.Workout
{
    public abstract class GenericWorkout
    {
        public static GenericWorkout Factory(string filename)
        {
            if (filename.EndsWith(".js"))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(JsonWorkout));
                var array = File.ReadAllBytes(filename);
                string json = Encoding.UTF8.GetString(array);
                Console.WriteLine(json);
                return (JsonWorkout)serializer.ReadObject(new MemoryStream(array));
            }
            else
            {
                return new ErgWorkout();
            }
        }

        public abstract void Update();

    }
}