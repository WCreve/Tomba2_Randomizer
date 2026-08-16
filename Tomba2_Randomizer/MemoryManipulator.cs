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

    const uint STRING_COURAGE_HALF = 0x80160415;
    const uint STRING_COURAGE_FULL = 0x80160430;
    const uint STRING_STRENGTH_HALF = 0x8016047B;
    const uint STRING_STRENGTH_FULL = 0x80160497;
    const uint STRING_WISDOM_HALF = 0x801604E5;
    const uint STRING_WISDOM_FULL = 0x801604FF;
    const uint STRING_HARP = 0x80161144;

    private IntPtr baseAddress;
    private Process _process;
    private IntPtr handle;

    private IntPtr basePtr; //0xb0000

    private IntPtr globalPtr; //0x40000
    private IntPtr binPtr; //0x110000
    private IntPtr switchPtr; //0x10000

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

        ReadProcessMemory((int)handle, baseAddress + 0x0F3E2A78, buffer, buffer.Length, out bytesRead);

        IntPtr ptr4 = IntPtr.Add(BitConverter.ToInt32(buffer), 0x8);
        ReadProcessMemory((int)handle, (int)ptr4, buffer, buffer.Length, out bytesRead);
        switchPtr = BitConverter.ToInt32(buffer);
    }

    public bool ProcessIsActive { get; set; } = true;

    public bool IgnoreChanges { get; set; }

    public bool IsActive { get; set; }

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

        if (!IsActive)
        {
            InitializeGame();

            Task.Run(CheckForUpdates);
            Task.Run(PerformChecks);
            Task.Run(ClearQueueHistories);

            IsActive = true;
        }
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

        WriteMemory(0xa514, new byte[12], globalPtr); //disable magic wings X2 popup

        WriteMemory(0xa534, new byte[12], globalPtr); //disable chick food X2 popup

        WriteMemory(0xa55c, new byte[12], globalPtr); //disable potato X3 popup

        WriteMemory(0x5660, [172, 167, 4, 128], switchPtr); //disable all spell item pickup logic
        WriteMemory(0x5668, [172, 167, 4, 128], switchPtr);
        WriteMemory(0x5670, [172, 167, 4, 128], switchPtr);

        WriteMemory(0x5720, [172, 167, 4, 128, 172, 167, 4, 128, 172, 167, 4, 128, 172, 167, 4, 128], switchPtr); //disable all harp piece pickup logic

        WriteMemory(0x28380, new byte[32], globalPtr); //disable attaching crab basket to tomba on area load

        WriteMemory(0xd338, [12, 128, 2, 60, 176, 248, 68, 160, 12, 128, 2, 60, 177, 248, 69, 160], globalPtr); //override AddInventoryQuantity function
        WriteMemory(0xd348, new byte[372], globalPtr);
        WriteMemory(0xd4bc, [8, 0, 224, 3], globalPtr);
        WriteMemory(0xd4c0, new byte[4], globalPtr);

        WriteMemory(0xd4d8, [12, 128, 2, 60, 2, 0, 3, 36, 178, 248, 67, 160], globalPtr); //override AddItemWithMessage function

        WriteMemory(0xf8b3, 1); //initialized
    }

    private async void CheckForUpdates() 
    {
        while (ProcessIsActive && randomizer != null)
        {
            if (ReadMemory(0xfb83) == 0) InitializeGame();

            var itemPickedUp = ReadMemory(0xf8b0, 3);

            var newIgt = ReadTimer();

            if (newIgt < igt) //rewind detected
            {
                HandleRewind(newIgt);
            }
            if (Math.Abs(igt - newIgt) > 500) //savestate loaded
            {
                HandleRewind(newIgt);
            }
            else if (itemPickedUp[0] != 0)
            {
                if (IgnoreChanges)
                {
                    AddItemWithMessage(itemPickedUp[0], itemPickedUp[1]);
                }
                else
                {
                    var custom = false;
                    var newItemId = randomizer.RandomizedItems.First(r => r.Key.InternalId == itemPickedUp[0]).Value.InternalId;

                    switch (itemPickedUp[0])
                    {
                        case 11: //pants
                        case 12:
                            newItemId = randomizer.RandomizedItems.First(r => r.Key.InternalId == (ReadMemory(0xf870) == 0 ? 11 : 12)).Value.InternalId; //check which pants you're picking up based on current area
                            break;

                        case 36: //star-shaped cog collected
                            SetFlagStarShapedCog();
                            break;

                        case 40: //pink bucket
                            if (!(ReadMemory(0xf870) == 0 && ReadMemory(0x37eaa) == 1 && BitConverter.ToInt16(ReadMemory(0x37eae, 2)) > 9000))
                            {
                                continue; //Only randomize the correct pink bucket pickup
                            }
                            break;

                        case 41: //crab basket
                            if ((ReadMemory(0xfadd) <= 1 || ReadMemory(0xf8bb) == 255) && newItemId != 41) //if no crab basket in inventory, or all crabs have been caught, re-disable crab catching
                            {
                                WriteMemory(0xf9e5, 7);
                            }

                            WriteMemory(0xc954, new byte[96], binPtr); //disable spawning basket pig when raising bridge
                            WriteMemory(0xc9c0, new byte[4], binPtr);
                            break;

                        case 42:
                        case 43:
                        case 44: //golden crab
                            switch (ReadMemory(0xf9e3) - crabsObtained)
                            {
                                case 1:
                                    newItemId = randomizer.RandomizedItems.First(r => r.Key.Id == 42).Value.InternalId;
                                    crabsObtained++;
                                    break;
                                case 2:
                                    newItemId = randomizer.RandomizedItems.First(r => r.Key.Id == 41).Value.InternalId;
                                    crabsObtained += 2;
                                    break;
                                case 4:
                                    newItemId = randomizer.RandomizedItems.First(r => r.Key.Id == 40).Value.InternalId;
                                    crabsObtained += 4;
                                    break;
                            }

                            break;

                        case 56:
                        case 57: //red/blue chick pickup checks
                            var chickStatus = ReadMemory(0xf9f2);

                            if (chickStatus == 136) newItemId = randomizer.RandomizedItems.First(i => i.Key.InternalId == 56).Value.InternalId; //Player has picked up 2 red chicks
                            if (chickStatus == 204) newItemId = randomizer.RandomizedItems.First(i => i.Key.InternalId == 57).Value.InternalId; //Player has picked up 2 blue chicks

                            break;

                        case 58: //rare fish collected
                            SetFlagRareFish();
                            break;

                        case 97: //blue bucket
                            if (!(ReadMemory(0xf870) == 1))
                            {
                                continue; //Only randomize the correct blue bucket pickup (expand later when working on pipe area)
                            }
                            break;

                        default:
                            break;
                    }

                    switch (newItemId)
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
                            WriteMemory(0xf88c, newItemId); //auto-equip weapon
                            WriteMemory(0x37eec, newItemId);
                            break;

                        case 11: //pants
                        case 12:
                            var pantsFound = ReadMemory(0xf9cf);
                            newItemId = (byte)(pantsFound == 0 ? 11 : 12);

                            WriteMemory(0xf9cf, ++pantsFound);
                            break;

                        case 40: //random pink bucket received
                            if (ReadMemory(0xf8b8) == 255) //give blue bucket instead of pink if Save the Crab is completed
                            {
                                newItemId = 97;
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

                        case 41: //crab basket
                            WriteMemory(0xf9e5, (byte)(ReadMemory(0xf8bb) == 255 ? 7 : 6));
                            break;

                        case 77: //chick food
                            AddItemWithoutMessage(77, 2);
                            QueuePopupMessage(87, 2, 66);
                            custom = true;
                            break;

                        case 96: //magic wings
                            if (itemPickedUp[1] == 2)
                            {
                                AddItemWithoutMessage(96, 2);
                                QueuePopupMessage(137, 2, 66);
                                custom = true;
                            }
                            break;

                        case 112: //spell of courage
                            if (ReadMemory(0xfb24) == 0)
                            {
                                AddItemWithoutMessage(112, 1);
                                QueueResourceMessage(STRING_COURAGE_HALF, 65);
                            }
                            else
                            {
                                AddItemWithoutMessage(111, 1);
                                RemoveItemWithoutMessage(112, 1);
                                QueueResourceMessage(STRING_COURAGE_FULL, 65);
                            }
                            custom = true;
                            break;
                        case 114: //spell of strength
                            if (ReadMemory(0xfb26) == 0)
                            {
                                AddItemWithoutMessage(114, 1);
                                QueueResourceMessage(STRING_STRENGTH_HALF, 65);
                            }
                            else
                            {
                                AddItemWithoutMessage(113, 1);
                                RemoveItemWithoutMessage(114, 1);
                                QueueResourceMessage(STRING_STRENGTH_FULL, 65);
                            }
                            custom = true;
                            break;
                        case 116: //spell of wisdom
                            if (ReadMemory(0xfb28) == 0)
                            {
                                AddItemWithoutMessage(116, 1);
                                QueueResourceMessage(STRING_WISDOM_HALF, 65);
                            }
                            else
                            {
                                AddItemWithoutMessage(115, 1);
                                RemoveItemWithoutMessage(116, 1);
                                QueueResourceMessage(STRING_WISDOM_FULL, 65);
                            }
                            custom = true;
                            break;

                        case 124: //potato
                            if (itemPickedUp[1] == 3)
                            {
                                AddItemWithoutMessage(124, 3);
                                QueuePopupMessage(138, 2, 66);
                                custom = true;
                            }
                            break;

                        case 160: //harp pieces
                        case 161:
                        case 162:
                        case 163:
                            var harpPieces = ReadMemory(0xfb54, 4);
                            if (harpPieces.Count(c => c != 0) == 3)
                            {
                                CompleteEvent(44);

                                AddItemWithMessage(newItemId, 1);

                                for (var i = 0; i < harpPieces.Length; i++)
                                {
                                    RemoveItemWithoutMessage((byte)(i + 160), 1);
                                }

                                AddItemWithoutMessage(164, 1);
                                QueueResourceMessage(STRING_HARP, 65);

                                custom = true;
                            }
                            else
                            {
                                AddItemWithMessage(newItemId, 1);
                            }
                            break;

                        default:
                            break;
                    }

                    if (!custom) AddItemWithMessage(newItemId, 1);
                }

                WriteMemory(0xf8b0, [0, 0, 0]);
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

        IsActive = false;
    }

    public void AddItemWithMessage(byte itemId, byte amount)
    {
        AddInventoryQuantity(itemId, amount);
        QueuePopupMessage(itemId, 2, 66);
    }

    private void AddItemWithoutMessage(byte itemId, byte amount) => AddInventoryQuantity(itemId, amount);

    private void AddInventoryQuantity(byte itemId, byte amount)
    {
        if (itemId > 22 && itemId < 29) 
        {
            var pigBagsStatusAmount = ReadMemory(0xf883);
            WriteMemory(0xf884 + pigBagsStatusAmount, itemId);
            WriteMemory(0xf883, (byte)(pigBagsStatusAmount + 1));
            EnablePigDoor(itemId);
        }

        var itemCounts = ReadMemory(0xfab4, 168);
        if (itemCounts[itemId] == 0)
        {
            var itemData = ReadMemory(0x62be8, 0xc00, globalPtr);
            var isTopItem = itemData[itemId * 0xc] == 0;

            for (int i = 0; i < 168; i++)
            {
                if (itemCounts[i] != 0 && (isTopItem ? itemData[i * 0xc] == 0 : itemData[i * 0xc] != 0))
                {
                    WriteMemory(0xfbb4 + i, (byte)(ReadMemory(0xfbb4 + i) + 1));
                }
            }
            WriteMemory(0xfbb4 + itemId, 0);

            WriteMemory(isTopItem ? 0xf8a2 : 0xf8a1, (byte)(ReadMemory(isTopItem ? 0xf8a2 : 0xf8a1) + 1));
        }

        var newItemCount = itemCounts[itemId] + amount;
        WriteMemory(0xfab4 + itemId, (byte)(newItemCount > 99 ? 99 : newItemCount));
    }

    public void RemoveItemWithMessage(byte itemId, byte amount)
    {
        WriteMemory(0xfab4 + itemId, (byte)(ReadMemory(0xfab4 + itemId) - amount));
        QueuePopupMessage(itemId, 1, 65);
        CompactInventoryAfterRemoval(itemId);
    }

    public void RemoveItemWithoutMessage(byte itemId, byte amount)
    {
        WriteMemory(0xfab4 + itemId, (byte)(ReadMemory(0xfab4 + itemId) - amount));
        CompactInventoryAfterRemoval(itemId);
    }

    private void CompactInventoryAfterRemoval(byte itemId)
    {
        var itemCounts = ReadMemory(0xfab4, 168);

        if (itemCounts[itemId] == 0)
        {
            var itemData = ReadMemory(0x62be8, 0xc00, globalPtr);
            var itemPositions = ReadMemory(0xfbb4, 168);
            WriteMemory(0xfbb4 + itemId, 0);
            var isTopItem = itemData[itemId * 0xc] == 0;

            for (int i = 0; i < 168; i++)
            {
                if ((itemPositions[itemId] < itemPositions[i]) && (isTopItem ? itemData[i * 0xc] == 0 : itemData[i * 0xc] != 0))
                {
                    WriteMemory(0xfbb4 + i, (byte)(itemPositions[i] - 1));
                }
            }

            WriteMemory(isTopItem ? 0xf8a2 : 0xf8a1, (byte)(ReadMemory(isTopItem ? 0xf8a2 : 0xf8a1) - 1));
        }
    }

    public void QueuePopupMessage(byte item, byte type, byte color)
    {
        var pendingPopupCount = ReadMemory(0xf552);
        if (pendingPopupCount < 8)
        {
            var ptrPopupQueue = 0xf6fc + pendingPopupCount * 0x20;

            WriteMemory(ptrPopupQueue, [item, 0, type, 128, 254, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, color]);
            WriteMemory(0xf552, (byte)(pendingPopupCount + 1));
        }
    }

    public void QueueResourceMessage(uint ptr, byte color)
    {
        var pendingPopupCount = ReadMemory(0xf552);
        if (pendingPopupCount < 8)
        {
            var ptrPopupQueue = 0xf6f8 + pendingPopupCount * 0x20;

            WriteMemory(ptrPopupQueue, BitConverter.GetBytes(ptr));
            WriteMemory(ptrPopupQueue + 4, [255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, color]);
            WriteMemory(0xf552, (byte)(pendingPopupCount + 1));
        }
    }

    private void CompleteEvent(byte eventId)
    {
        if (ReadMemory(0x37fee) != 0) //check if tomba alive
        {
            var ptrEventState = 0xf8b4 + eventId;
            var eventState = ReadMemory(ptrEventState);

            if (eventState == 0) WriteMemory(0xf8a8, (byte)(ReadMemory(0xf8a8) + 1)); //increase started events by 1
            if (eventState != 255)
            {
                WriteMemory(ptrEventState, 255);
                WriteMemory(0xf8aa, (byte)(ReadMemory(0xf8aa) + 1)); //increase completed events by 1
                var eventAPReward = GetEventAPReward(eventId, true);

                var currentAP = BitConverter.ToInt32(ReadMemory(0xf874, 4)); //update AP
                WriteMemory(0xf874, BitConverter.GetBytes(currentAP + eventAPReward));

                var pendingEventNotificationCount = ReadMemory(0x3d06d);
                WriteMemory(0x3d06e + pendingEventNotificationCount, eventId); //event popup
                WriteMemory(0x3d074 + pendingEventNotificationCount, 1);
                WriteMemory(0x3d06d, (byte)(pendingEventNotificationCount + 1));
            }
        }
    }

    private int GetEventAPReward(byte eventId, bool completed) => ReadMemory(0x63b38 + (completed ? (ReadMemory(0x633c9 + eventId * 12, globalPtr) & 15) : (ReadMemory(0x633c9 + eventId * 12, globalPtr) >> 4)) * 4, globalPtr);

    public byte[] ReadMemory(int ptrAddress, int amountOfBytes, IntPtr ptr)
    {
        IntPtr bytesRead = 0;
        IntPtr readPtr = IntPtr.Add(ptr, ptrAddress);

        var bytes = new byte[amountOfBytes];
        ReadProcessMemory((int)handle, (int)readPtr, bytes, bytes.Length, out bytesRead);

        return bytes;
    }

    public byte ReadMemory(int ptrAddress, IntPtr ptr) => ReadMemory(ptrAddress, 1, ptr)[0];

    public byte[] ReadMemory(int ptrAddress, int amountOfBytes) => ReadMemory(ptrAddress, amountOfBytes, basePtr);

    public byte ReadMemory(int ptrAddress) => ReadMemory(ptrAddress, 1, basePtr)[0];

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

    public void WriteMemory(int address, byte[] values, IntPtr ptr)
    {
        IntPtr bytesWritten = 0;

        IntPtr textPtr = IntPtr.Add(ptr, address);
        WriteProcessMemory((int)handle, (int)textPtr, values, values.Count(), out bytesWritten);
    }

    public void WriteMemory(int address, byte value, IntPtr ptr, bool ignoreRandom = false) => WriteMemory(address, [value], ptr);

    public void WriteMemory(int address, byte[] values, bool ignoreRandom = false) => WriteMemory(address, values, basePtr);

    public void WriteMemory(int address, byte value, bool ignoreRandom = false) => WriteMemory(address, [value], basePtr);

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

    private void EnablePigDoor(int id)
    {
        switch (ReadMemory(0xf870)) //un-hide pig door if player collects pig bag corresponding to current level
        {
            case 0:
                if (id == 27) WriteMemory(0x4e81d, 2);
                break;
            case 1:
            case 4:
                if (id == 23 || id == 26) WriteMemory(0x4e26d, 2);
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
}