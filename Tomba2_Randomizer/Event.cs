using System.Collections.Generic;

namespace Tomba2_Randomizer;

public class Event
{
    public byte Id
    {
        get
        {
            return (byte)(Address - 0xf8b4);
        }
    }

    public int Address { get; set; }

    public string Name { get; set; }

    public bool Primary { get; set; }

    public int AP { get; set; }

    public bool Unlocked { get; set; }

    public List<RequirementGroup> RequirementGroups { get; set; } = [];
}