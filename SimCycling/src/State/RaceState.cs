using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Numerics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;


namespace SimCycling.State
{

    [DataContract]
    public struct SerializableVector3
    {
        [DataMember]
        public float X;
        [DataMember]
        public float Y;
        [DataMember]
        public float Z;

        public SerializableVector3(float rx, float ry, float rz)
        {
            X = rx;
            Y = ry;
            Z = rz;
        }

        public static implicit operator Vector3(SerializableVector3 rValue)
        {
            return new Vector3(rValue.X, rValue.Y, rValue.Z);
        }

        public static implicit operator SerializableVector3(Vector3 rValue)
        {
            return new SerializableVector3(rValue.X, rValue.Y, rValue.Z);
        }
    }

    [DataContract]
    public class RaceState
    {
        private static MemoryMappedFile mm;

        [DataMember(Name = "car_positions")]
        public List<SerializableVector3> CarPositions { get; set; } = new List<SerializableVector3>(); // Position of all cars in the race, 0 is the player

        [DataMember(Name = "car_velocities")]
        public List<SerializableVector3> CarVelocities { get; set; } = new List<SerializableVector3>();

        [DataMember(Name = "normalized_car_positions")]
        public List<float> NormalizedCarPositions { get; set; } = new List<float>();


        public static RaceState Instance { get; private set; } = new RaceState();


        public static byte[] TrimEnd(byte[] array)
        {
            int lastIndex = Array.FindLastIndex(array, b => b != 0);

            Array.Resize(ref array, lastIndex + 1);

            return array;
        }

        public static void ReadFromMemory()
        {
            mm = MemoryMappedFile.CreateOrOpen("SimCyclingRaceState", 1024);

            using (var mmAccessor = mm.CreateViewAccessor())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(RaceState));
                byte[] array = Enumerable.Repeat((byte)0x0, 1024).ToArray();
                mmAccessor.ReadArray<byte>(0, array, 0, 1024);
                array = TrimEnd(array);
                string json = Encoding.UTF8.GetString(array);
                RaceState newState;
                if (array.Length > 0)
                {
                    try
                    {
                        newState = (RaceState)serializer.ReadObject(new MemoryStream(array));
                        Instance = newState;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }
        }
    }
}