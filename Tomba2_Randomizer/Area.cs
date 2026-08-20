using System.Collections.Generic;

namespace Tomba2_Randomizer;

public class Area
{
    public int Id { get; set; }

    public string Name { get; set; }

    public bool Unlocked { get; set; }

    public List<RequirementGroup> RequirementGroups { get; set; } = new();
    public List<RequirementGroup> ImportantGroups { get; set; } = new();
}