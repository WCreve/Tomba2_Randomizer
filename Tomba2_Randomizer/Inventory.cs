namespace Tomba2_Randomizer;

public class Inventory
{
    public AddressValuePair[] Counts { get; set; }

    public AddressValuePair[] Positions { get; set; }

    public override bool Equals(object? obj)
    {
        if (obj is not Inventory inventory) return false;

        for (var i = 0; i < Counts.Length; i++)
        {
            if (!Counts[i].Equals(inventory.Counts[i]))
            {
                return false;
            }
            if (!Positions[i].Equals(inventory.Positions[i]))
            {
                return false;
            }
        }

        return true;
    }
}