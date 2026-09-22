using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tomba2_Randomizer
{
    public class AppData
    {
        public List<RandomizerSeed> SeedHistory { get; set; } = [];

        public List<RandomizerSeed> GetRecentSeeds()
        {
            var seedsShownCount = SeedHistory.Count > 10 ? 10 : SeedHistory.Count;

            return SeedHistory.OrderByDescending(s => s.Timestamp).Take(seedsShownCount).ToList();
        }
    }
}
