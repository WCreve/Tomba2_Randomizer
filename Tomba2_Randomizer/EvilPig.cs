using System.Collections.Generic;

namespace Tomba2_Randomizer
{
    public class EvilPig
    {
        public int Id { get; set; }
        public byte Area { get; set; }
        public byte PigArea { get; set; }
        public byte VictoryWarp { get; set; }
        public byte Event { get; set; }
        public string PigBagName { get; set; }
        public byte PigBagId { get; set; }
        public short CLUT { get; set; }
        public int CLUTStartCursed { get; set; }
        public int CLUTStartPurified { get; set; }
        public int DoorDataAddress { get; set; }
        public int DoorReturnWarpAddress { get; set; }
        public bool InInterior { get; set; }
        public List<PigDoorCoordinates> Coordinates { get; set; }
    }
}
