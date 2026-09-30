namespace Tomba2_Randomizer
{
    public class Coordinates
    {
        public byte WarpIndex { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public ushort Z { get; set; }
        public byte Scene { get; set; }
        public byte Direction { get; set; }
        public bool Active { get; set; }
    }
}
