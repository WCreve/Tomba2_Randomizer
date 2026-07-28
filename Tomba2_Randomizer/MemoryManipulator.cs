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

    private byte[] prevItemCounts;

    private int igt;

    private bool warping;
    private byte[] warpDestination;

    private Queue<AddressValuePair> writeQueue;

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

        writeQueue = new Queue<AddressValuePair>();

        Task.Run(CheckForUpdates);
        Task.Run(PerformChecks);
    }

    private async void CheckForUpdates()
    {
        while (ProcessIsActive && randomizer != null)
        {
            var itemCounts = ReadMemory(0xfab4, 168);

            if (igt > ReadTimer() || Math.Abs(igt - ReadTimer()) > 50 || IgnoreChanges) //rewinds/savestate loads
            {
                prevItemCounts = itemCounts;
            }
            else if (!itemCounts.SequenceEqual(prevItemCounts))
            {
                for (int i = 0; i < itemCounts.Length; i++)
                {
                    if (itemCounts[i] != prevItemCounts[i])
                    {
                        var itemPair = randomizer.RandomizedItems.FirstOrDefault(r => r.Key.CountAddress == i + 0xfab4);

                        if (itemPair.Value != null)
                        {
                            if (itemPair.Key.Id == 64 || itemPair.Key.Id == 65) //red/blue chick pickup checks
                            {
                                itemPair = ModifyChickPickup(itemPair);
                            }
                            else if (itemPair.Key.Id == 66) //rare fish collected
                            {
                                SetFlagRareFish();
                            }

                            ItemPopup(itemPair.Key, itemPair.Value, prevItemCounts[i]);
                        }
                    }
                }
                prevItemCounts = ReadMemory(0xfab4, 168);
            }

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

    public byte[] ReadMemory(int ptrAddress, int amountOfBytes)
    {
        IntPtr bytesRead = 0;
        IntPtr ptr = IntPtr.Add(basePtr, ptrAddress);

        var bytes = new byte[amountOfBytes];
        ReadProcessMemory((int)handle, (int)ptr, bytes, bytes.Length, out bytesRead);

        return bytes;
    }

    public byte ReadMemory(int ptrAddress) => ReadMemory(ptrAddress, 1)[0];

    public byte ReadInventoryTopBottomAmount(bool top) => ReadMemory(top ? 0xf8a2 : 0xf8a1);

    public byte ReadPopupAmount() => ReadMemory(0xf550);

    public byte[] ReadEvents()
    {
        var eventList = ReadMemory(0xf8b5, 138).ToList();
        eventList.RemoveAt(62); //event index 62 is unused

        return eventList.ToArray();
    }

    public byte[] ReadProgress() => ReadMemory(0xf9b4, 168);

    private int ReadTimer()
    {
        igt = BitConverter.ToInt32(ReadMemory(0xf878, 4));

        return igt;
    }

    public void WriteMemory(int address, byte[] values, bool isManual = false)
    {
        if (isManual) IgnoreChanges = true;

        IntPtr bytesWritten = 0;

        IntPtr textPtr = IntPtr.Add(basePtr, address);
        WriteProcessMemory((int)handle, (int)textPtr, values, values.Count(), out bytesWritten);
    }

    public void WriteMemory(int address, byte value, bool isManual = false) => WriteMemory(address, [value], isManual);

    public void WriteProgress(byte[] bytes) => WriteMemory(0xf9b4, bytes);

    public void WriteInventoryTopBottomAmount(bool top, byte amount) => WriteMemory(top ? 0xf8a2 : 0xf8a1, amount);

    public void Teleport(byte area, byte section) => WriteMemory(0xf839, [1, section, area]);

    public bool IsGameRunning()
    {
        int igt = ReadTimer();
        var checkByte = ReadMemory(0xf9d0);

        return igt > 0 || checkByte == 95;
    }

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

                    while (writeQueue.Any())
                    {
                        var avp = writeQueue.Dequeue();
                        WriteMemory(avp.Address, avp.Value, true);
                    }
                }
            }

            var customByte1 = ReadMemory(0xf9c1);

            if (((customByte1 >> 0) & 1) == 0) GiveStarterWings(customByte1);

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
                    writeQueue.Enqueue(new AddressValuePair { Address = 0xfaee, Value = rareFishInInventory });
                    WriteMemory(0xfaee, 0, true);
                }
                if (((rareFishDelivered >> 0) & 1) == 1) // rare fish delivered -> temporarily set flag to false, then change back after loading in
                {
                    writeQueue.Enqueue(new AddressValuePair { Address = 0xf9c0, Value = rareFishDelivered });
                    WriteMemory(0xf9c0, 0);
                }
            }
            else if (rareFishInInventory == 0) // stop rare fish from appearing if grabbed and no fish in inventory
            { 
                writeQueue.Enqueue(new AddressValuePair { Address = 0xfaee, Value = 0 });
                WriteMemory(0xfaee, 1, true);
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

    private KeyValuePair<Item, Item> ModifyChickPickup(KeyValuePair<Item, Item> kvp)
    {
        //If a player picks up 2 of the same chick, the second one needs to give the item corresponding to the other color chick
        var chickStatus = ReadMemory(0xf9f2);

        if (chickStatus == 136) return new KeyValuePair<Item, Item>(kvp.Key, randomizer.RandomizedItems.First(i => i.Key.Id == 64).Value); //Player has picked up 2 red chicks
        if (chickStatus == 204) return new KeyValuePair<Item, Item>(kvp.Key, randomizer.RandomizedItems.First(i => i.Key.Id == 65).Value); //Player has picked up 2 blue chicks

        return kvp;
    }

    private void SetFlagRareFish() => WriteMemory(0xf9c1, (byte)(ReadMemory(0xf9c1) | 0b_0000_0010));

}