using System.Collections.Generic;

namespace Tomba2_Randomizer;

public class RequirementGroup
{
    public List<Item> Items { get; set; } = [];
    public List<Area> Areas { get; set; } = [];
    public List<Event> Events { get; set; } = [];
    public int AP { get; set; } = new();
    public bool Active { get; set; }
    public byte SpecialRequirementGroup { get; set; }
}