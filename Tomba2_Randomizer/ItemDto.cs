using System;
using System.Collections.Generic;

namespace Tomba2_Randomizer;

public class ItemDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string GUIName
    {
        get
        {
            return string.IsNullOrEmpty(DisplayName) ? Name : DisplayName;
        }
    }
    public string Color { get; set; }
    public string Address { get; set; }
    public bool NotRandom { get; set; }

    public byte InternalId
    {
        get
        {
            return (byte)(Convert.ToInt32(Address, 16) - 0xfab4);
        }
    }

    public List<RequirementDto> Requirements { get; set; }
}