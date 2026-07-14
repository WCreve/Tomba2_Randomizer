using System.Collections.Generic;

namespace Tomba2_Randomizer;

public class EventDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Address { get; set; }
    public int AP { get; set; }
    public bool Primary { get; set; }
    public List<RequirementDto> Requirements { get; set; }
}