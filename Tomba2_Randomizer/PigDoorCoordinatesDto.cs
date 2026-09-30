using System.Collections.Generic;

namespace Tomba2_Randomizer
{
    public class PigDoorCoordinates
    {
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public ushort Z { get; set; }
        public ushort ReturnX { get; set; }
        public ushort ReturnY { get; set; }
        public ushort ReturnZ { get; set; }
        public byte ReturnScene { get; set; }
        public byte ReturnDirection { get; set; }
        public List<RequirementDto> Requirements { get; set; } = [];
        public bool Active { get; set; }
    }
}
