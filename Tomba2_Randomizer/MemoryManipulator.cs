using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Tomba2_Randomizer;

public class MemoryManipulator
{
    const int PROCESS_ALL_ACCESS = 0x1F0FFF;

    private IntPtr baseAddress;
    private Process _process;
    private IntPtr handle;

    private IntPtr basePtr;
    private IntPtr binPtr;

    private byte[] prevItemCounts;

    private int igt;

    private bool warping;

    private List<QueuedChange> writeQueueWarp;
    private List<QueuedChange> writeQueueSafe;

    private Randomizer randomizer;

    public MemoryManipulator(Process process)
    {
        _process = process;
        baseAddress = _process.MainModule.BaseAddress;
        handle = OpenProcess(PROCESS_ALL_ACCESS, false, _process.Id);
        _process.Exited += TombaProcessClosed;

        IntPtr bytesRead = 0;
        byte[] buffer = new byte[8];

        ReadProcessMemory((int)handle, baseAddress + 0x0F3E2A78, buffer, buffer.Length, out bytesRead);

        IntPtr ptr = IntPtr.Add(BitConverter.ToInt32(buffer), 0x58);
        ReadProcessMemory((int)handle, (int)ptr, buffer, buffer.Length, out bytesRead);
        basePtr = BitConverter.ToInt32(buffer);

        ReadProcessMemory((int)handle, baseAddress + 0x0F3E2A78, buffer, buffer.Length, out bytesRead);

        IntPtr ptr2 = IntPtr.Add(BitConverter.ToInt32(buffer), 0x188);
        ReadProcessMemory((int)handle, (int)ptr2, buffer, buffer.Length, out bytesRead);
        binPtr = BitConverter.ToInt32(buffer);
    }

    public bool ProcessIsActive { get; set; } = true;

    public bool IgnoreChanges { get; set; }

    [DllImport("kernel32.dll")]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

    private void TombaProcessClosed(object? sender, EventArgs e)
    {
        _process.Dispose();
    }

    public void SetupRandomizer(Randomizer r)
    {
        randomizer = r;
        prevItemCounts = new byte[168];

        IgnoreChanges = true;

        writeQueueWarp = new List<QueuedChange>();
        writeQueueSafe = new List<QueuedChange>();

        Task.Run(CheckForUpdates);
        Task.Run(PerformChecks);
        Task.Run(ClearQueueHistories);
    }

    private async void CheckForUpdates() 
    {
        while (ProcessIsActive && randomizer != null)
        {
            var itemCounts = ReadMemory(0xfab4, 168);
            var newIgt = ReadTimer();

            if (newIgt < igt) //rewind detected
            {
                HandleRewind(newIgt);
                prevItemCounts = itemCounts;
            }
            else if (Math.Abs(igt - newIgt) > 50 || IgnoreChanges) //savestate loaded
            {
                prevItemCounts = itemCounts;
            }
            else if (!itemCounts.SequenceEqual(prevItemCounts))
            {
                for (int i = 0; i < itemCounts.Length; i++)
                {
                    if (itemCounts[i] > prevItemCounts[i])
                    {
                        var itemPair = randomizer.RandomizedItems.FirstOrDefault(r => r.Key.CountAddress == i + 0xfab4);

                        if (itemPair.Value != null)
                        {
                            var key = itemPair.Key;
                            var value = itemPair.Value;

                            switch (key.Id)
                            {
                                case 21:
                                case 22:
                                case 23:
                                case 24:
                                case 25:
                                case 26:
                                    var amountOfBags = ReadMemory(0xf883);

                                    WriteMemory(0xf883 + amountOfBags, 0);
                                    WriteMemory(0xf883, --amountOfBags);
                                    break;

                                case 34: //star-shaped cog collected
                                    SetFlagStarShapedCog();
                                    break;

                                case 38:
                                    if (!(ReadMemory(0xf870) == 0 && ReadMemory(0x37eaa) == 1 && BitConverter.ToInt16(ReadMemory(0x37eae, 2)) > 9000))
                                    {
                                        continue; //Only randomize the correct pink bucket pickup
                                    }
                                    break;

                                case 64:
                                case 65: //red/blue chick pickup checks
                                    value = ModifyChickPickup(key.Id);
                                    break;

                                case 66: //rare fish collected
                                    SetFlagRareFish();
                                    break;

                                case 98:
                                    if (!(ReadMemory(0xf870) == 1))
                                    {
                                        continue; //Only randomize the correct blue bucket pickup (expand later when working on pipe area)
                                    }
                                    break;

                                default:
                                    break;
                            }

                            switch (value.Id)
                            {
                                case 21:
                                case 22:
                                case 23:
                                case 24:
                                case 25:
                                case 26:
                                    PigBagObtained(value.Id);
                                    break;

                                case 38: //random pink bucket received
                                    if (ReadMemory(0xf8b8) == 255) //give blue bucket instead of pink if Save the Crab is completed
                                    {
                                        value = randomizer.RandomizedItems.Values.First(r => r.Id == 98);
                                    }
                                    else if (ReadMemory(0xf870) == 0) //auto-equip pink bucket if in starting beach
                                    {
                                        var pairs = new List<AddressValuePair>
                                    {
                                        new AddressValuePair { Address = 0xf88e, Value = 40 },
                                        new AddressValuePair { Address = 0xf81c, Value = 1 },
                                        new AddressValuePair { Address = 0x37e85, Value = 17 }
                                    };

                                        writeQueueSafe.Add(new QueuedChange(ReadTimer(), pairs));
                                    }
                                    break;
                                default:
                                    break;
                            }

                            ItemPopup(key, value, prevItemCounts[i]);
                        }
                    }
                }
                prevItemCounts = ReadMemory(0xfab4, 168);
            }

            if (ReadMemory(0x37e85) == 0)
            {
                foreach (var item in writeQueueSafe.Where(i => i.DequeueTimeStamp == 0))
                {
                    foreach (var pair in item.AddressValuePairs)
                    {
                        WriteMemory(pair.Address, pair.Value, true);
                    }
                    item.DequeueTimeStamp = newIgt;
                }
            }

            igt = newIgt;
            IgnoreChanges = false;
            Thread.Sleep(17);
        }
    }

    private void ItemPopup(Item oldItem, Item newItem, byte oldItemCount)
    {
        var popupCount = ReadPopupAmount();
        var popupPtr = popupCount == 1 ? 0xF564 : 0xF5F0;

        List<byte> boxText = [(byte)newItem.Color];

        for (int i = 0; i < newItem.DisplayName.Length; i++)
        {
            if (newItem.DisplayName[i] == ' ') boxText.Add(251);
            else boxText.Add((byte)(newItem.DisplayName[i] - 32));
        }
        boxText.AddRange([240, 251, 65, 67, 81, 85, 73, 82, 69, 68, 1, 255]);

        WriteMemory(popupPtr, boxText.ToArray());

        WriteMemory(popupPtr - 0x10, (byte)(120 - newItem.DisplayName.Length * 4));

        WriteMemory(popupPtr - 0xC, (byte)(80 + newItem.DisplayName.Length * 8));

        WriteMemory(oldItem.CountAddress, oldItemCount);

        var inventory = ReadInventory();

        if (oldItemCount == 0)
        {
            var invAmount = ReadInventoryTopBottomAmount(oldItem.Color == ItemColor.Green);

            WriteInventoryTopBottomAmount(oldItem.Color == ItemColor.Green, (byte)(invAmount - 1));
            WriteMemory(oldItem.PositionAddress, 0);

            if (oldItem.Color == ItemColor.Green != (newItem.Color == ItemColor.Green))
            {
                var oldItemPositions = oldItem.Color == ItemColor.Green ? inventory.Positions.Where(p => p.Address <= 0xfbca) : inventory.Positions.Where(p => p.Address > 0xfbca);

                byte oldItemIndex = 0;
                foreach (var pos in oldItemPositions.OrderBy(p => p.Value))
                {
                    if (inventory.Counts.First(c => c.Address == pos.Address - 256).Value != 0)
                    {
                        WriteMemory(pos.Address, oldItemIndex++);
                    }
                }
            }
        }

        var newItemCount = ReadMemory(newItem.CountAddress);

        if (newItemCount == 0)
        {
            var invAmount = ReadInventoryTopBottomAmount(newItem.Color == ItemColor.Green);

            WriteInventoryTopBottomAmount(newItem.Color == ItemColor.Green, (byte)(invAmount + 1));

            var positions = newItem.Color == ItemColor.Green ? inventory.Positions.Where(p => p.Address <= 0xfbca) : inventory.Positions.Where(p => p.Address > 0xfbca);

            var index = newItemCount == 0 ? 1 : 0;
            foreach (var pos in positions.OrderBy(p => p.Value))
            {
                if (inventory.Counts.First(c => c.Address == pos.Address - 256).Value != 0)
                {
                    WriteMemory(pos.Address, (byte)index++);
                }
            }
        }

        

        WriteMemory(newItem.CountAddress, (byte)(newItemCount + 1));
    }

    public Inventory ReadInventory()
    {
        var itemCounts = ReadMemory(0xfab4, 168);
        var itemPositions = ReadMemory(0xfbb4, 168);

        if (itemCounts.Length > 0)
        {
            var inventory = new Inventory
            {
                Counts = new AddressValuePair[168],
                Positions = new AddressValuePair[168]
            };

            for (int i = 0; i < itemCounts.Length; i++)
            {
                inventory.Counts[i] = new AddressValuePair
                {
                    Address = 0xfab4 + i,
                    Value = itemCounts[i]
                };

                inventory.Positions[i] = new AddressValuePair
                {
                    Address = 0xfbb4 + i,
                    Value = itemPositions[i]
                };
            }

            return inventory;
        }

        return new Inventory();
    }

    public byte[] ReadMemory(int ptrAddress, int amountOfBytes, bool binMemory = false)
    {
        IntPtr bytesRead = 0;
        IntPtr ptr = IntPtr.Add(binMemory ? binPtr : basePtr, ptrAddress);

        var bytes = new byte[amountOfBytes];
        ReadProcessMemory((int)handle, (int)ptr, bytes, bytes.Length, out bytesRead);

        return bytes;
    }

    public byte ReadMemory(int ptrAddress, bool binMemory = false) => ReadMemory(ptrAddress, 1, binMemory)[0];

    public byte ReadInventoryTopBottomAmount(bool top) => ReadMemory(top ? 0xf8a2 : 0xf8a1);

    public byte ReadPopupAmount() => ReadMemory(0xf550);

    public byte[] ReadEvents()
    {
        var eventList = ReadMemory(0xf8b5, 138).ToList();
        eventList.RemoveAt(62); //event index 62 is unused

        return eventList.ToArray();
    }

    public byte[] ReadProgress() => ReadMemory(0xf9b4, 168);

    private int ReadTimer() => BitConverter.ToInt32(ReadMemory(0xf878, 4));

    public void WriteMemory(int address, byte[] values, bool ignoreRandom = false, bool binMemory = false)
    {
        if (ignoreRandom) IgnoreChanges = true;

        IntPtr bytesWritten = 0;

        IntPtr textPtr = IntPtr.Add(binMemory ? binPtr : basePtr, address);
        WriteProcessMemory((int)handle, (int)textPtr, values, values.Count(), out bytesWritten);
    }

    public void WriteMemory(int address, byte value, bool ignoreRandom = false, bool binMemory = false) => WriteMemory(address, [value], ignoreRandom, binMemory);

    public void WriteProgress(byte[] bytes) => WriteMemory(0xf9b4, bytes);

    public void WriteInventoryTopBottomAmount(bool top, byte amount) => WriteMemory(top ? 0xf8a2 : 0xf8a1, amount);

    public void Teleport(byte area, byte section) => WriteMemory(0xf839, [1, section, area]);

    public bool IsGameRunning() => ReadTimer() > 0 || ReadMemory(0xf9d0) == 95;

    public async Task PerformChecks()
    {
        while (ProcessIsActive)
        {
            var isWarping = ReadMemory(0xf839) != 0;
            if (!warping)
            {
                if (isWarping) WarpChecks();
            }
            else
            {
                if (!isWarping)
                {
                    warping = false;

                    Thread.Sleep(100);

                    switch (ReadMemory(0xf870)) //current area
                    {
                        case 0:
                            WriteMemory(0x7c30, [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], binMemory: true); //disable pink bucket auto-equip
                            break;
                        default:
                            break;
                    }

                    foreach (var item in writeQueueWarp.Where(i => i.DequeueTimeStamp == 0))
                    {
                        foreach (var pair in item.AddressValuePairs)
                        {
                            WriteMemory(pair.Address, pair.Value, true);
                        }
                        item.DequeueTimeStamp = ReadTimer();
                    }

                    var customByte1 = ReadMemory(0xf9c1);

                    if (((customByte1 >> 0) & 1) == 0) GiveStarterWings(customByte1);
                }
            }

           

            Thread.Sleep(100);
        }
    }

    private void WarpChecks()
    {
        warping = true;

        var warpDestination = ReadMemory(0xf83a, 2);

        if (warpDestination[1] == 0)
        {
            var rareFishGrabbed = ((ReadMemory(0xf9c1) >> 1) & 1) == 1;
            var rareFishInInventory = ReadMemory(0xfaee);
            var rareFishDelivered = ReadMemory(0xf9c0);

            if (!rareFishGrabbed) // rare fish hasn't been grabbed
            {
                if (rareFishInInventory > 0) // rare fish in inventory -> temporarily remove until area loaded
                {
                    writeQueueWarp.Add(new QueuedChange(ReadTimer(), new AddressValuePair { Address = 0xfaee, Value = rareFishInInventory }));
                    WriteMemory(0xfaee, 0, true);
                }
                if (((rareFishDelivered >> 0) & 1) == 1) // rare fish delivered -> temporarily set flag to false, then change back after loading in
                {
                    writeQueueWarp.Add(new QueuedChange(ReadTimer(), new AddressValuePair { Address = 0xf9c0, Value = rareFishDelivered }));
                    WriteMemory(0xf9c0, 0);
                }
            }
            else if (rareFishInInventory == 0) // stop rare fish from appearing if grabbed and no fish in inventory
            { 
                writeQueueWarp.Add(new QueuedChange(ReadTimer(), new AddressValuePair { Address = 0xfaee, Value = 0 }));
                WriteMemory(0xfaee, 1, true);
            }

            var cogGrabbed = ((ReadMemory(0xf9c1) >> 2) & 1) == 1;
            var cogInInventory = ReadMemory(0xfad8);
            var windItUpCompleted = ReadMemory(0xf8b9);

            if (!cogGrabbed) // star-shaped cog hasn't been grabbed
            {
                if (cogInInventory > 0) // star-shaped cog in inventory -> temporarily remove until area loaded
                {
                    writeQueueWarp.Add(new QueuedChange(ReadTimer(), new AddressValuePair { Address = 0xfad8, Value = cogInInventory }));
                    WriteMemory(0xfad8, 0, true);
                }
                if (windItUpCompleted == 255) // wind it up event completed -> temporarily set to not started, then change back after loading in
                {
                    writeQueueWarp.Add(new QueuedChange(ReadTimer(), new AddressValuePair { Address = 0xf8b9, Value = windItUpCompleted }));
                    WriteMemory(0xf8b9, 0);
                }
            }
            else if (cogInInventory == 0 && windItUpCompleted != 255) // stop star-shaped cog from appearing if grabbed and no cog in inventory and event not completed
            {
                writeQueueWarp.Add(new QueuedChange(ReadTimer(), new AddressValuePair { Address = 0xfad8, Value = 0 }));
                WriteMemory(0xfad8, 1, true);
            }

            if (warpDestination[0] != 0 && warpDestination[0] != 9) //Entering waterfall area of starting beach
            {
                if (ReadMemory(0xf9dd) < 11)
                {
                    WriteMemory(0xf9dd, 11); //Can now jump over unraised net bridge
                }
            }
        }

        var pigDoorsOpened = ReadMemory(0xfa17);

        //Prepare pig doors in case player gets the pig bag for that area in that area
        if ((warpDestination[0] == 0 && (((pigDoorsOpened >> 4) & 1) != 1)) || (warpDestination[0] == 1 && (((pigDoorsOpened >> 2) & 1) != 1)) || (warpDestination[0] == 4 && (((pigDoorsOpened >> 1) & 1) != 1)))
        {
            var bags = ReadMemory(0xf884, 6);
            var bagList = bags.ToList();

            if ((warpDestination[0] == 0 && !(bagList.Contains(27) || bagList.Contains(155))) || (warpDestination[0] == 1 && !(bagList.Contains(23) || bagList.Contains(151))) || (warpDestination[0] == 4 && !(bagList.Contains(24) || bagList.Contains(152))))
            {
                var bagCount = ReadMemory(0xf883);

                var pairs = new List<AddressValuePair>()
                    {
                        new AddressValuePair { Address = 0xf883, Value = bagCount },
                        new AddressValuePair { Address = 0xf884, Value = bags[0] },
                        new AddressValuePair { Address = 0xf885, Value = bags[1] },
                        new AddressValuePair { Address = 0xf886, Value = bags[2] },
                        new AddressValuePair { Address = 0xf887, Value = bags[3] },
                        new AddressValuePair { Address = 0xf888, Value = bags[4] },
                        new AddressValuePair { Address = 0xf889, Value = bags[5] },
                        new AddressValuePair { Address = warpDestination[0] == 0 ? 0x4e81d : 0x4e26d, Value = 4 },
                    };
                writeQueueWarp.Add(new QueuedChange(ReadTimer(), pairs));

                WriteMemory(0xf883, [6, 23, 24, 25, 26, 27, 28]);
            }
        }

        if (ReadMemory(0xfadc) > 0) 
        {
            if (warpDestination[1] == 0) //equip pink bucket if present in inventory and in starting beach
            {
                WriteMemory(0xf88e, 40);
                WriteMemory(0x37eee, 40);
            }
            else
            {
                if (ReadMemory(0xf88e) == 40) //unequip pink bucket if present in inventory and not in starting beach
                {
                    WriteMemory(0xf88e, 0);
                    WriteMemory(0x37eee, 0);
                }
            }
            
        }
    }

    private void GiveStarterWings(byte bits)
    {
        if (ReadMemory(0xfb14) == 0)
        {
            var amountOfBottomItems = ReadInventoryTopBottomAmount(false);

            WriteInventoryTopBottomAmount(false, (byte)(amountOfBottomItems + 1));

            WriteMemory(0xfc14, amountOfBottomItems);
        }
        WriteMemory(0xfb14, 80);

        bits = (byte)(bits | 0b_0000_0001);

        WriteMemory(0xf9c1, bits);
    }

    private Item ModifyChickPickup(int key)
    {
        //If a player picks up 2 of the same chick, the second one needs to give the item corresponding to the other color chick
        var chickStatus = ReadMemory(0xf9f2);

        if (chickStatus == 136) return randomizer.RandomizedItems.First(i => i.Key.Id == 64).Value; //Player has picked up 2 red chicks
        if (chickStatus == 204) return randomizer.RandomizedItems.First(i => i.Key.Id == 65).Value; //Player has picked up 2 blue chicks

        return randomizer.RandomizedItems.First(i => i.Key.Id == key).Value;
    }

    private void PigBagObtained(int id)
    {
        var amountOfBags = ReadMemory(0xf883);
        WriteMemory(0xf883, ++amountOfBags);

        WriteMemory(0xf883 + amountOfBags, (byte)(id + 2));

        switch (ReadMemory(0xf870)) //un-hide pig door if player collects pig bag corresponding to current level
        {
            case 0:
                if (id == 25) WriteMemory(0x4e81d, 2);
                break;
            case 1:
            case 4:
                if (id == 21 || id == 24) WriteMemory(0x4e26d, 2);
                break;
            default:
                break;

        }
        if (ReadMemory(0xf870) == 0)
        {
            WriteMemory(0x4e81d, 2);
        }
    }

    private void SetFlagRareFish() => WriteMemory(0xf9c1, (byte)(ReadMemory(0xf9c1) | 0b_0000_0010));
    private void SetFlagStarShapedCog() => WriteMemory(0xf9c1, (byte)(ReadMemory(0xf9c1) | 0b_0000_0100));

    private void HandleRewind(int time)
    {
        writeQueueSafe.RemoveAll(i => i.EnqueueTimeStamp > time);
        writeQueueWarp.RemoveAll(i => i.EnqueueTimeStamp > time);

        foreach (var item in writeQueueSafe.Where(i => i.DequeueTimeStamp != 0))
        {
            if (time < item.DequeueTimeStamp)
            {
                item.DequeueTimeStamp = 0;
            }
        }
    }

    private void ClearQueueHistories()
    {
        while (ProcessIsActive)
        {
            writeQueueSafe.RemoveAll(i => i.DequeueTimeStamp != 0 && igt - i.DequeueTimeStamp > 50000);
            writeQueueWarp.RemoveAll(i => i.DequeueTimeStamp != 0 && igt - i.DequeueTimeStamp > 50000);

            Thread.Sleep(10000);
        }
    }
}