using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tomba2_Randomizer
{
    public class AppData
    {
        private List<RandomizerSeed> _seedHistory;

        public int ItemsPerRow { get; set; } = 16;

        public List<RandomizerSeed> SeedHistory 
        {
            get
            {
                return _seedHistory;
            }
            set
            {
                if (value.Count > 50) value.RemoveRange(0, value.Count - 50);
                _seedHistory = value;
            }
        }

        public List<RandomizerSeed> GetRecentSeeds()
        {
            var seedsShownCount = SeedHistory.Count > 10 ? 10 : SeedHistory.Count;

            return SeedHistory.OrderByDescending(s => s.Timestamp).Take(seedsShownCount).ToList();
        }
    }
}
