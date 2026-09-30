using System.Collections.Generic;

namespace Tomba2_Randomizer
{
    public class WarpCoordinate
    {
        public byte Area { get; set; }
        public int Ptr { get; set; }
        public List<Coordinates> Coordinates { get; set; }
    }
}
