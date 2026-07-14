using System.Collections.Generic;

namespace Tomba2_Randomizer;

public class RequirementGroup
{
    public List<Item> Items { get; set; } = new();
    public List<Area> Areas { get; set; } = new();
    public List<Event> Events { get; set; } = new();
    public int AP { get; set; } = new();
}