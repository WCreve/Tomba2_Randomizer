using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Tomba2_Randomizer
{
    public class RandomizerSeed
    {
        public RandomizerSeed(int seed, long timestamp)
        {
            Seed = seed;
            Timestamp = timestamp;
        }

        public int Seed { get; private set; }

        public long Timestamp { get; set; }

        [JsonIgnore]
        public string SeedString
        {
            get
            {
                return ToString();
            }
        }

        public override string ToString()
        {
            return $"{Seed}   ({new DateTime(Timestamp):yyyy-MM-dd HH:mm:ss})";
        }
    }
}
