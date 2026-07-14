using System.Collections.Generic;

namespace Tomba2_Randomizer;

public class RequirementDto
{
    public List<int> Items { get; set; }
    public List<int> Areas { get; set; }
    public List<int> Events { get; set; }
    public int AP { get; set; }
}