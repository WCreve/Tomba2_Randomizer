using System.Collections.Generic;

namespace Tomba2_Randomizer;

public class Item
{
    public int Id { get; set; }

    public string Name { get; set; }
    public string DisplayName { get; set; }

    public int CountAddress { get; set; }

    public int PositionAddress
    {
        get
        {
            return CountAddress + 256;
        }
    }

    public byte InternalId
    {
        get
        {
            return (byte)(CountAddress - 0xfab4);
        }
    }

    public ItemColor Color { get; set; }

    public bool Important { get; set; }

    public bool NotRandom { get; set; }

    public List<RequirementGroup> RequirementGroups { get; set; } = new();
}