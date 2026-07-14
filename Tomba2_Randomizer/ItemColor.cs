using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Tomba2_Randomizer;

[JsonConverter(typeof(StringEnumConverter))]
public enum ItemColor
{
    [EnumMember(Value = "Blue")]
    Blue = 242,
    [EnumMember(Value = "Pink")]
    Pink = 243,
    [EnumMember(Value = "Green")]
    Green = 244
}