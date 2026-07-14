using System.Collections.Generic;

namespace Tomba2_Randomizer;

public class AreaDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<RequirementDto> Requirements { get; set; }
}