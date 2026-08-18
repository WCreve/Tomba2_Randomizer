using System;

namespace Tomba2_Randomizer;

public class AddressValuePair
{
    public int Address { get; set; } = 0;
    public byte Value { get; set; } = 0;
    public IntPtr Ptr { get; set; }

    public override bool Equals(object? obj)
    {
        if (obj is not AddressValuePair pair) return false;

        if (Address != pair.Address)
        {
            return false;
        }
        if (Value != pair.Value)
        {
            return false;
        }

        return true;
    }
}