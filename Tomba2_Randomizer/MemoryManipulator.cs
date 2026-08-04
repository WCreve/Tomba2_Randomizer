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

    private IntPtr globalPtr;
    private IntPtr binPtr;

    private byte[] prevItemCounts;

    private int igt;

    private bool warping;
    private bool interiorTransition;

    private List<QueuedChange> writeQueueWarp;
    private List<QueuedChange> writeQueueSafe;

    private Randomizer randomizer;

    private byte crabsObtained;

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

        IntPtr ptr2 = IntPtr.Add(BitConverter.ToInt32(buffer), 0x20);
        ReadProcessMemory((int)handle, (int)ptr2, buffer, buffer.Length, out bytesRead);
        globalPtr = BitConverter.ToInt32(buffer);

        ReadProcessMemory((int)handle, baseAddress + 0x0F3E2A78, buffer, buffer.Length, out bytesRead);

        IntPtr ptr3 = IntPtr.Add(BitConverter.ToInt32(buffer), 0x188);
        ReadProcessMemory((int)handle, (int)ptr3, buffer, buffer.Length, out bytesRead);
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

        InitializeGame();

        Task.Run(CheckForUpdates);
        Task.Run(PerformChecks);
        Task.Run(ClearQueueHistories);
    }

    private void InitializeGame()
    {
        writeQueueWarp = new List<QueuedChange>();
        writeQueueSafe = new List<QueuedChange>();

        WriteMemory(0xa468, new byte[12], globalPtr); //disable auto-equipping weapons on pickup
        WriteMemory(0xa478, new byte[4], globalPtr);
        WriteMemory(0xa494, new byte[4], globalPtr);
        WriteMemory(0xa4a0, new byte[4], globalPtr);

        WriteMemory(0xa4b8, new byte[8], globalPtr); //disable auto-equipping pants and incrementing pants found counter on pickup
        WriteMemory(0xa4c4, new byte[4], globalPtr);
        WriteMemory(0xa4d4, new byte[44], globalPtr);
        WriteMemory(0xa504, new byte[4], globalPtr);

        WriteMemory(0xa570, new byte[224], globalPtr); //disable all 1/2 spell item pickup logic

        WriteMemory(0x28380, new byte[32], globalPtr); //disable attaching crab basket to tomba on area load
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
            else if (Math.Abs(igt - newIgt) > 500) //savestate loaded
            {
                prevItemCounts = itemCounts;
                InitializeGame();
            }
            else if (IgnoreChanges) prevItemCounts = itemCounts;
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
                                case 10: //pants
                                case 11:
                                    value = randomizer.RandomizedItems.First(r => r.Key.Id == (ReadMemory(0xf870) == 0 ? 10 : 11)).Value; //check which pants you're picking up based on current area
                                    break;

                                case 21: //pig bags
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

                                case 38: //pink bucket
                                    if (!(ReadMemory(0xf870) == 0 && ReadMemory(0x37eaa) == 1 && BitConverter.ToInt16(ReadMemory(0x37eae, 2)) > 9000))
                                    {
                                        continue; //Only randomize the correct pink bucket pickup
                                    }
                                    break;

                                case 39: //crab basket
                                    if ((ReadMemory(0xfadd) <= 1 || ReadMemory(0xf8bb) == 255) && value.Id != 39) //if no crab basket in inventory, or all crabs have been caught, re-disable crab catching
                                    {
                                        WriteMemory(0xf9e5, 7);
                                    }

                                    WriteMemory(0xc954, new byte[96], binPtr); //disable spawning basket pig when raising bridge
                                    WriteMemory(0xc9c0, new byte[4], binPtr);
                                    break;

                                case 40:
                                case 41:
                                case 42: //golden crab
                                    switch (ReadMemory(0xf9e3) - crabsObtained)
                                    {
                                        case 1:
                                            value = randomizer.RandomizedItems.First(r => r.Key.Id == 42).Value;
                                            crabsObtained++;
                                            break;
                                        case 2:
                                            value = randomizer.RandomizedItems.First(r => r.Key.Id == 41).Value;
                                            crabsObtained += 2;
                                            break;
                                        case 4:
                                            value = randomizer.RandomizedItems.First(r => r.Key.Id == 40).Value;
                                            crabsObtained += 4;
                                            break;
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
                                case 1: //weapons
                                case 2:
                                case 3:
                                case 4:
                                case 5:
                                case 6:
                                case 7:
                                case 8:
                                case 9:
                                    WriteMemory(0xf88c, (byte)value.Id); //auto-equip weapon
                                    WriteMemory(0x37eec, (byte)value.Id);
                                    break;

                                case 10: //pants
                                case 11:
                                    var pantsFound = ReadMemory(0xf9cf);
                                    value = randomizer.Items[pantsFound == 0 ? 10 : 11];

                                    WriteMemory(0xf9cf, ++pantsFound);
                                    break;

                                case 21: //pig bags
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
                                        value = randomizer.Items[98];
                                    }
                                    else if (ReadMemory(0xf870) == 0) //auto-equip pink bucket if in starting beach
                                    {
                                        var bucketPairs = new List<AddressValuePair>
                                        {
                                            new AddressValuePair { Address = 0xf88e, Value = 40 },
                                            new AddressValuePair { Address = 0xf81c, Value = 1 },
                                            new AddressValuePair { Address = 0x37e85, Value = 17 }
                                        };
                                        Enqueue(writeQueueSafe, bucketPairs);
                                    }
                                    break;

                                case 39: //crab basket
                                    WriteMemory(0xf9e5, (byte)(ReadMemory(0xf8bb) == 255 ? 7 : 6));
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

    public void WriteMemory(int address, byte[] values, IntPtr ptr, bool ignoreRandom = false)
    {
        if (ignoreRandom) IgnoreChanges = true;

        IntPtr bytesWritten = 0;

        IntPtr textPtr = IntPtr.Add(ptr, address);
        WriteProcessMemory((int)handle, (int)textPtr, values, values.Count(), out bytesWritten);
    }

    public void WriteMemory(int address, byte value, IntPtr ptr, bool ignoreRandom = false) => WriteMemory(address, [value], ptr, ignoreRandom);

    public void WriteMemory(int address, byte[] values, bool ignoreRandom = false) => WriteMemory(address, values, basePtr, ignoreRandom);

    public void WriteMemory(int address, byte value, bool ignoreRandom = false) => WriteMemory(address, [value], basePtr, ignoreRandom);

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

                switch (ReadMemory(0xf870)) //current area
                {
                    case 0:
                        var enteringInterior = ReadMemory(0xf817, 2);

                        if (!interiorTransition)
                        {
                            if (enteringInterior[0] == 2 && enteringInterior[1] == 1 && ReadMemory(0xf8bc) != 255) //entering windmill while windmill event incomplete
                            {
                                interiorTransition = true;
                                var obtainedCrabs = ReadMemory(0xf9e3);
                                WriteMemory(0xf9e2, obtainedCrabs); //temporarily store information about obtained crabs
                                var crabsInInventory = ReadMemory(0xfade);

                                obtainedCrabs = (byte)(obtainedCrabs >> 4 << 4);

                                for (int i = 0; i < 3; i++)
                                {
                                    if (crabsInInventory > 0 && ((obtainedCrabs >> i + 4) & 1) != 1)
                                    {
                                        obtainedCrabs |= (byte)Math.Pow(2, i);
                                        crabsInInventory--;
                                    }
                                }

                                WriteMemory(0xf9e3, obtainedCrabs);

                            }
                            else if (enteringInterior[0] == 2 && enteringInterior[1] == 3 && ReadMemory(0xf8bc) != 255) //leaving windmill
                            {
                                interiorTransition = true;

                                var tempCrabInfo = ReadMemory(0xf9e2, 2);

                                if (ReadMemory(0xf8bc) != 255 || tempCrabInfo[0] != 0) //store the correct crab data in 0xbf9e3
                                {
                                    WriteMemory(0xf9e2, [0, (byte)((tempCrabInfo[1] & 0xF0) | (tempCrabInfo[0] & 0x0F))]);
                                }
                            }
                        }
                        else if (enteringInterior[1] == 2 || enteringInterior[1] == 4) interiorTransition = false;
                    break;
                }
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
                            WriteMemory(0x7c30, [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], binPtr); //disable pink bucket auto-equip
                            WriteMemory(0x6f34, [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 2, 36, 1, 0, 2, 162, 0, 0, 0, 0, 0, 0, 0, 0], binPtr); //disable attaching crab basket to tomba
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
                    Enqueue(writeQueueWarp, new AddressValuePair { Address = 0xfaee, Value = rareFishInInventory });
                    WriteMemory(0xfaee, 0, true);
                }
                if (((rareFishDelivered >> 0) & 1) == 1) // rare fish delivered -> temporarily set flag to false, then change back after loading in
                {
                    Enqueue(writeQueueWarp, new AddressValuePair { Address = 0xf9c0, Value = rareFishDelivered });
                    WriteMemory(0xf9c0, 0);
                }
            }
            else if (rareFishInInventory == 0) // stop rare fish from appearing if grabbed and no fish in inventory
            {
                Enqueue(writeQueueWarp, new AddressValuePair { Address = 0xfaee, Value = 0 });
                WriteMemory(0xfaee, 1, true);
            }

            var cogGrabbed = ((ReadMemory(0xf9c1) >> 2) & 1) == 1;
            var cogInInventory = ReadMemory(0xfad8);
            var windItUpCompleted = ReadMemory(0xf8b9);

            if (!cogGrabbed) // star-shaped cog hasn't been grabbed
            {
                if (cogInInventory > 0) // star-shaped cog in inventory -> temporarily remove until area loaded
                {
                    Enqueue(writeQueueWarp, new AddressValuePair { Address = 0xfad8, Value = cogInInventory });
                    WriteMemory(0xfad8, 0, true);
                }
                if (windItUpCompleted == 255) // wind it up event completed -> temporarily set to not started, then change back after loading in
                {
                    Enqueue(writeQueueWarp, new AddressValuePair { Address = 0xf8b9, Value = windItUpCompleted });
                    WriteMemory(0xf8b9, 0);
                }
            }
            else if (cogInInventory == 0 && windItUpCompleted != 255) // stop star-shaped cog from appearing if grabbed and no cog in inventory and event not completed
            {
                Enqueue(writeQueueWarp, new AddressValuePair { Address = 0xfad8, Value = 0 });
                WriteMemory(0xfad8, 1, true);
            }

            if (warpDestination[0] != 0 && warpDestination[0] != 9) //Entering waterfall area of starting beach
            {
                if (ReadMemory(0xf9dd) < 11)
                {
                    WriteMemory(0xf9dd, 11); //Can now jump over unraised net bridge
                    WriteMemory(0xf9e5, 2); //Spawn pig holding crab basket and crabs
                }
            }

            crabsObtained = ReadMemory(0xf9e3);

            if (ReadMemory(0xfadd) > 0) //player has crab basket in inventory
            {
                if (ReadMemory(0xf8ba) != 255) //The Crab Basket event not completed
                {
                    WriteMemory(0xf9e5, 2); //spawn basket-holding pig
                }
                else if (ReadMemory(0xf8bb) != 255) // Collect the Golden Crabs event not completed
                {
                    WriteMemory(0xf9e5, 6); //enable crab catching
                }
                else
                {
                    WriteMemory(0xf9e5, 7); //disable crab catching
                }
            }

            if (ReadMemory(0xf9e5) > 0) //disable spawning basket pig when raising bridge if pig has already been spawned/basket has been collected.
            {

                WriteMemory(0xc954, new byte[96], binPtr); 
                WriteMemory(0xc9c0, new byte[4], binPtr);
            }

            var tempCrabInfo = ReadMemory(0xf9e2, 2);

            if (tempCrabInfo[0] != 0)
            {
                WriteMemory(0xf9e2, [0, (byte)((tempCrabInfo[1] & 0xF0) | (tempCrabInfo[0] & 0x0F))]);
            }
        }

        var pigDoorsOpened = ReadMemory(0xfa17);

        //Prepare pig doors in case player gets the pig bag for that area in that area
        if ((warpDestination[1] == 0 && (((pigDoorsOpened >> 4) & 1) != 1)) || (warpDestination[1] == 1 && (((pigDoorsOpened >> 2) & 1) != 1)) || (warpDestination[1] == 4 && (((pigDoorsOpened >> 1) & 1) != 1)))
        {
            var bags = ReadMemory(0xf884, 6);
            var bagList = bags.ToList();

            if ((warpDestination[1] == 0 && !(bagList.Contains(27) || bagList.Contains(155))) || (warpDestination[1] == 1 && !(bagList.Contains(23) || bagList.Contains(151))) || (warpDestination[1] == 4 && !(bagList.Contains(24) || bagList.Contains(152))))
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
                        new AddressValuePair { Address = warpDestination[1] == 0 ? 0x4e81d : 0x4e26d, Value = 4 },
                    };

                Enqueue(writeQueueWarp, pairs);
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

        foreach (var item in writeQueueWarp.Where(i => i.DequeueTimeStamp != 0))
        {
            if (time < item.DequeueTimeStamp)
            {
                item.DequeueTimeStamp = 0;
            }
        }

        crabsObtained = ReadMemory(0xf9e3);
    }

    public void Enqueue(List<QueuedChange> queue, List<AddressValuePair> pairs) => queue.Add(new QueuedChange(ReadTimer(), pairs));
    public void Enqueue(List<QueuedChange> queue, AddressValuePair pair) => Enqueue(queue, new List<AddressValuePair> { pair });

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