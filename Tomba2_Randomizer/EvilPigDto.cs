using System;
using System.Collections.Generic;
using System.Text;

namespace Tomba2_Randomizer
{
    public class EvilPigDto
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
        public bool InInterior { get; set; }
        public List<RequirementDto> Requirements { get; set; } = [];
    }
}
