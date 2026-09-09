using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
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
    const uint STRING_CUSTOM = 0x80160E08;

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
    private List<QueuedChange> writeQueuePopup;

    private Randomizer randomizer;

    private byte loadedBin;

    private bool conversationScriptPaused;

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
            writeQueueWarp = new List<QueuedChange>();
            writeQueueSafe = new List<QueuedChange>();
            writeQueuePopup = new List<QueuedChange>();

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
        writeQueuePopup = new List<QueuedChange>();

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

        WriteMemory(0x2838c, 73, globalPtr); //disable attaching crab basket to tomba on area load

        WriteMemory(0xd338, [12, 128, 2, 60, 176, 248, 68, 160, 12, 128, 2, 60, 177, 248, 69, 160], globalPtr); //override AddInventoryQuantity function
        WriteMemory(0xd348, new byte[372], globalPtr);
        WriteMemory(0xd4bc, [8, 0, 224, 3], globalPtr);
        WriteMemory(0xd4c0, new byte[4], globalPtr);

        WriteMemory(0xd4d8, [12, 128, 2, 60, 2, 0, 3, 36, 178, 248, 67, 160], globalPtr); //override AddItemWithMessage function

        WriteMemory(0xf9ca, 2); //start at kujara wash level 3

        WriteMemory(0xf8b3, 1); //initialized
    }

    private async void CheckForUpdates() 
    {
        while (ProcessIsActive && randomizer != null)
        {
            var currentBin = ReadMemory(-0x7064, binPtr);
            if (currentBin != loadedBin)
            {
                if (ReadMemory(0xf8b3) == 0) InitializeGame();
                WriteMemory(0xf8ad, [1, 1, 1]); //re-enable T1 events because LRG's fix for these events is weird
                WriteMemory(0xf8ac, (byte)(ReadMemory(0xf8c8) == 255 ? 1 : 0)); //enable dwarf event if big sack event completed;
                loadedBin = currentBin;
                EditBinMemory();
            }

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
                        case 7: //fire hammer
                            WriteMemory(0xf8ac, 1); //enable Tomba 1 dwarf event
                            break;

                        case 11: //pants collected
                        case 12:
                            newItemId = randomizer.RandomizedItems.First(r => r.Key.InternalId == (ReadMemory(0xf870) == 0 ? 11 : 12)).Value.InternalId; //check which pants you're picking up based on current area
                            break;

                        case 17: //swimming pig suit collected
                            if (newItemId != 17 && ReadMemory(0xfac5) == 0)
                            {
                                WriteMemory(0xf9e2, 1); //temporary flag to destroy platform when leaving room
                                var dialogueScriptOffset = BitConverter.ToUInt16(ReadMemory(0x4240c, 2)) + 0x4006C;
                                WriteMemory(dialogueScriptOffset, [12, 106]); //skip to end of mermaid dialogue script

                                if (ReadMemory(0xfac4) == 0)
                                {
                                    AddItemWithoutMessage(16, 1);
                                    QueueCustomPopup("Another {P}Pig Suit{W} magically{n}appears in your inventory.{n}Lucky you!");
                                }
                            }

                            WriteMemory(0x2d240, 21, binPtr); //enable giant fish cutscene
                            break;

                        case 19: //evil ice pig robe collected
                            if (ReadMemory(0xf8c9) != 255) //melt the giant ice not completed
                            {
                                CompleteEvent(21, false);
                                WriteMemory(0xfa07, (byte)(ReadMemory(0xfa07) | 128)); //get rid of big ice pigs
                                WriteMemory(0xfa0a, 34); //get rid of big ice pigs
                            }

                            if (ReadMemory(0xf8cd) != 255) //static explosion not completed
                            {
                                CompleteEvent(25, false);
                                WriteMemory(0xf9c4, 55); //set all kujaras to delivered
                                WriteMemory(0xf9c6, 21); //pham cutscene completed
                                QueueCustomPopup("The {O}Donglin Forest lift{W} was fixed!");
                            }

                            if (ReadMemory(0xf8ce) != 255) //raise the ladder not completed
                            {
                                CompleteEvent(26, false);
                                if (ReadMemory(0xfad9) == 1) //if player has hexagon gear, remove
                                {
                                    QueueCustomPopup("Your {P}Hexagon Gear{W} was{n}used automatically!");
                                }
                                else
                                {
                                    SetFlagRemoveHexagonGear();
                                }
                            }

                            if (ReadMemory(0xf9c5) == 0) //summit ice block not pushed
                            {
                                WriteMemory(0xf9c5, 1); //move ice block into sculpturer's hut
                            }

                            break;

                        case 20: //evil ghost pig robe collected
                            if (ReadMemory(0xf8d0) != 255) //put in the spirit's eye not completed
                            {
                                CompleteEvent(28, false);
                            }

                            if (ReadMemory(0xf8d1) != 255) //kill the guards not completed
                            {
                                CompleteEvent(29, false);
                                AddItemWithMessage(167, 1, true);
                                //look into adding a donglin bell item pickup
                            }

                            if (ReadMemory(0xf8d5) != 255) //use rock crabs for balance not completed
                            {
                                CompleteEvent(33, false);
                                WriteMemory(0xfa22, 48);
                                WriteMemory(0xfa5a, 1);
                            }
                            break;

                        case 28: //last evil pig bag collected
                            if (!(ReadMemory(0xfac6, 5).Count(r => r == 1) == 5 && (ReadMemory(0xfad0) == 1 || newItemId == 28))) //prevent opening door to ??? if you don't have all robes and the last pig bag
                            {
                                WriteMemory(0xa8b8, [43, 0, 32, 162, 12, 128, 2, 60, 1, 0, 4, 36, 241, 249, 68, 160], binPtr);
                            }

                            var newItem = randomizer.RandomizedItems.First(r => r.Key.InternalId == itemPickedUp[0]).Value;
                            QueueCustomPopup((newItem.Color == ItemColor.Green ? "{G}" : newItem.Color == ItemColor.Pink ? "{P}" : "{B}") + newItem.DisplayName + "{W} acquired!{n}All magic power has been restored!{n}Magic power is infinite!");
                            newItemId = newItem.InternalId;
                            itemPickedUp[2] = 1;

                            break;

                        case 36: //star-shaped cog collected
                            if (ReadMemory(0x37eaa) == 16) //standing near net bridge
                            {
                                AddItemWithMessage(36, 1);
                                custom = true;
                            }
                            else
                            {
                                SetFlagStarShapedCog();
                            }
                            break;

                        case 37: //hexagon gear collected
                            if (ReadMemory(0x37eaa) == 4) //standing near ladder
                            {
                                AddItemWithMessage(37, 1);
                                custom = true;
                            }
                            break;

                        case 38: //triangle gear collected
                            if (ReadMemory(0xf817) == 7 && ReadMemory(0xf818) == 2) //in gear interior
                            {
                                AddItemWithMessage(38, 1);
                                custom = true;
                            }
                            else if ((ReadMemory(0xf9e2) & 4) == 4)
                            {
                                WriteMemory(0x3a14, 8, binPtr);
                                WriteMemory(0x1ad2c, 8, binPtr);
                                WriteMemory(0x1ba80, 7, binPtr);
                                WriteMemory(0xf9d6, 14);
                                WriteMemory(0xf9e2, (byte)(ReadMemory(0xf9e2) & ~(4 << 0)));
                                WriteMemory(0xfa45, (byte)(ReadMemory(0xfa45) | 1));
                            }
                            break;

                        case 39: //round cog collected
                            SetFlagRoundCog();
                            break;

                        case 40: //pink bucket
                            var usingItemPinkBucket = ReadMemory(0xf80a, 2);
                            if (usingItemPinkBucket[0] == 1 && usingItemPinkBucket[1] == 99) //no popup message if bucket received from using a full bucket
                            {
                                AddItemWithoutMessage(40, 1);
                                custom = true;
                            }
                            else if (!(ReadMemory(0xf870) == 0 && ReadMemory(0x37eaa) == 1 && BitConverter.ToInt16(ReadMemory(0x37eae, 2)) > 9000))
                            {
                                AddItemWithoutMessage(40, 1); //Only randomize the correct pink bucket pickup
                                custom = true;
                                break;
                            }
                            break;

                        case 42: //golden crab collected
                            var crabCaught = (ReadMemory(0xf9e3) - ReadMemory(0xf9e6)) & 7;

                            newItemId = randomizer.RandomizedItems.First(r => r.Key.Id == 42 - BitOperations.TrailingZeroCount(crabCaught)).Value.InternalId;

                            WriteMemory(0xf9e6, (byte)(ReadMemory(0xf9e6) + crabCaught));
                            break;

                        case 45: //big sack collected
                            SetFlagBigSack();
                            break;

                        case 48: //blue fruit collected
                            SetFlagBlueFruit();
                            break;

                        case 49: //rock crab collected
                            SetFlagRockCrab();

                            if (ReadMemory(0xf870) == 6) WriteMemory(0x58a0, [113, 86, 4, 8], binPtr); //block rock crab respawning

                            break;

                        case 50: //paon grass collected
                            if (ReadMemory(0xfae6) == 0 /*&& newItemId != 50*/) //skip rest of dialogue if player does not have paon grass to prevent visually equipping the paon grass
                            {
                                WriteMemory(ReadDialogueOffsetPtr(), [44, 87]);
                            }
                            break;

                        case 53: //pig nose panel collected
                            var currentArea = ReadMemory(0xf870);
                            var pigNoseAreas = new byte[] { 0, 1, 4, 6, 20 };

                            newItemId = randomizer.RandomizedItems.First(r => r.Key.Id == 53 + pigNoseAreas.IndexOf(currentArea)).Value.InternalId;

                            break;

                        case 54: //seeds of strength collected
                            var sessionSeedsCollected = ReadMemory(0x37eab);

                            if (itemPickedUp[2] == 2) //seed grabbed. item isn't given yet
                            {
                                var tempSeeds = 0;

                                if (BitOperations.PopCount(sessionSeedsCollected) == 1) tempSeeds = 0;
                                else tempSeeds = ReadMemory(0xf9e2);

                                QueueCustomPopupItem(randomizer.RandomizedItems.First(r => r.Key.Id == 58 + BitOperations.TrailingZeroCount(sessionSeedsCollected - tempSeeds)).Value, 1);

                                WriteMemory(0xf9e2, sessionSeedsCollected);
                            }
                            else //trolley reaches goal, give items
                            {
                                while (sessionSeedsCollected != 0)
                                {
                                    int seedNumber = BitOperations.TrailingZeroCount(sessionSeedsCollected);
                                    AddItemWithoutMessage(randomizer.RandomizedItems.First(r => r.Key.Id == 58 + seedNumber).Value.InternalId, 1);

                                    sessionSeedsCollected &= (byte)(sessionSeedsCollected - 1);
                                }

                                WriteMemory(0xf9e2, 0);
                            }

                            custom = true;
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

                        case 62: //hot dregs collected
                            if (ReadMemory(0x37eaa) == 2) //check tomba approx pos to see which dregs are collected.
                            {
                                newItemId = randomizer.RandomizedItems.First(r => r.Key.Id == 70).Value.InternalId;
                            }
                            else newItemId = randomizer.RandomizedItems.First(r => r.Key.Id == 71).Value.InternalId;
                            break;

                        case 63: //icy dregs collected
                            if (ReadMemory(0x37eaa) < 10) //check tomba approx pos to see which dregs are collected.
                            {
                                newItemId = randomizer.RandomizedItems.First(r => r.Key.Id == 73).Value.InternalId;
                            }
                            else newItemId = randomizer.RandomizedItems.First(r => r.Key.Id == 72).Value.InternalId;
                            break;

                        case 80: //snow firefly box collected
                            SetFlagSnowFireflyBox();
                            break;

                        case 82: //rucksack collected
                            SetFlagRucksack();
                            break;

                        case 97: //blue bucket collected
                            var usingItemBlueBucket = ReadMemory(0xf80a, 2);
                            if (usingItemBlueBucket[0] == 1 && usingItemBlueBucket[1] >= 99 && usingItemBlueBucket[1] <= 101) //no popup message if bucket received from using a full bucket
                            {
                                AddItemWithoutMessage(97, 1);
                                custom = true;
                            }
                            else if (!(ReadMemory(0xf870) == 1)) //blue bucket reward from trolley does get randomized
                            {
                                AddItemWithMessage(97, 1);
                                custom = true;
                            }
                            break;

                        case 98: //mermaid bucket
                            if (!(ReadMemory(0xf870) == 8 && ReadMemory(0xfe60) == 40)) //not in the trapped mermaid section of the water temple
                            {
                                AddItemWithoutMessage(itemPickedUp[0], 1);
                                custom = true;
                            }
                            break;

                        case 99: //water buckets
                        case 100:
                        case 101:
                            AddItemWithoutMessage(itemPickedUp[0], 1);
                            custom = true;
                            break;

                        case 105: //toradako oil collected
                            WriteMemory(0xf9ca, 9); //skip to level 10 kujara wash
                            break;

                        case 108: //clear fruit
                            SetFlagClearFuit();
                            WriteMemory(0x11e3c, 2, binPtr); //disable clear fruit pickup
                            break;

                        case 112: //1/2 spell of courage
                            newItemId = randomizer.RandomizedItems.First(i => i.Key.Id == (ReadMemory(0xf817) == 1 ? 106: 107)).Value.InternalId;
                            break;

                        case 114: //1/2 spell of strength
                            newItemId = randomizer.RandomizedItems.First(i => i.Key.Id == (ReadMemory(0xf870) == 4 ? 108 : 109)).Value.InternalId;
                            break;

                        case 116: //1/2 spell of wisdom
                            newItemId = randomizer.RandomizedItems.First(i => i.Key.Id == (ReadMemory(0xf870) == 5 ? 110 : 111)).Value.InternalId;
                            break;

                        case 128: //mudball surprise
                            if (ReadMemory(0x37eaa) > 20) //check tomba approx pos to see which mudball is being collected.
                            {
                                newItemId = randomizer.RandomizedItems.First(r => r.Key.Id == 112).Value.InternalId;
                            }
                            else newItemId = randomizer.RandomizedItems.First(r => r.Key.Id == 174).Value.InternalId;
                            break;

                        case 129: //low-purity items
                        case 130:
                            if(BitConverter.ToInt16(ReadMemory(0x37eae, 2)) < 9200) //don't randomize if retrieving from furnace as a result of not having delivered the power coal
                            {
                                AddItemWithMessage(itemPickedUp[0], 1);
                                custom = true;
                            }
                            break;

                        default:
                            break;
                        }

                    if (!custom)
                    {
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

                            case 18: //evil pig robes
                            case 19:
                            case 20:
                            case 21:
                            case 22:
                                if (ReadMemory(0xf870) == 1 && ReadMemory(0xfac6, 5).Count(r => r == 1) == 4 && ReadMemory(0xfad0) == 1) //allow unlocking door to ??? if this is your final evil pig robe and you have the last bag
                                {
                                    WriteMemory(0xa8b8, [6, 0, 37, 162, 1, 0, 4, 36, 213, 8, 1, 12, 33, 40, 128, 0], binPtr);
                                }
                                break;

                            case 28: //last pig bag
                                if (ReadMemory(0xf870) == 1 && ReadMemory(0xfac6, 5).Count(r => r == 1) == 5) //allow unlocking door to ??? if you have all the pig bags
                                {
                                    WriteMemory(0xa8b8, [6, 0, 37, 162, 1, 0, 4, 36, 213, 8, 1, 12, 33, 40, 128, 0], binPtr);
                                }
                                break;

                            case 37: //hexagon gear
                                if ((ReadMemory(0xf9c2) & 1) == 1)
                                {
                                    QueueCustomPopup("{P}Hexagon Gear{W} obtained and used!");
                                    WriteMemory(0xf9c2, (byte)(ReadMemory(0xf9c2) & ~(1 << 0)));
                                    custom = true;
                                }

                                if (ReadMemory(0xf870) == 5) //in summit
                                {
                                    if (ReadMemory(0xf937) != 0 && (ReadMemory(0xfa4b) & 2) == 0) //allowed to recollect gears and hexagon gear has not been recollected
                                    {
                                        WriteMemory(0x4e268, 1); //enable recollecting hexagon gear
                                        WriteMemory(0x182be, [80, 162], binPtr);
                                    }
                                }
                                break;

                            case 40: //random pink bucket received
                                if (ReadMemory(0xf8b8) == 255 || ReadMemory(0xfb15) != 0) //give blue bucket instead of pink if Save the Crab is completed or if you already have a blue bucket
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
                                WriteMemory(0xf9e5, (byte)(ReadMemory(0xf8bb) == 255 ? 7 : 6)); //enable crab catching unless all crabs have already been caught
                                break;

                            case 44: //trolley rail
                                if (ReadMemory(0xf870) == 2) //in CMT
                                {
                                    WriteMemory(0x10fd8, [10, 128, 5, 60, 90, 3, 1, 12, 192, 60, 165, 36, 2, 0, 2, 36, 112, 0, 98, 162, 254, 131, 4, 8, 1, 0, 2, 36], binPtr); //re-enable trolley cutscene
                                }

                                break;

                            case 45: //big sack
                                if (ReadMemory(0xf870) == 4) //in kujara ranch
                                {
                                    var bigSackActor = AllocateActorPool1(); //make big sack appear on back
                                    WriteMemory(bigSackActor + 2, 0x12);
                                    WriteMemory(bigSackActor + 3, 0);
                                    WriteMemory(bigSackActor + 0x1c, BitConverter.GetBytes(0x8011b674));
                                    WriteMemory(bigSackActor + 0x28, (byte)(ReadMemory(bigSackActor + 0x28) | 0x80));
                                }
                                break;

                            case 50: //paon grass
                                if (ReadMemory(0xf870) == 7) //in circus village
                                {
                                    var paonGrassActor = AllocateActorPool1(); //make paon grass appear on back
                                    WriteMemory(paonGrassActor + 2, 0x12);
                                    WriteMemory(paonGrassActor + 3, 0);
                                    WriteMemory(paonGrassActor + 0x1c, BitConverter.GetBytes(0x80117680));
                                    WriteMemory(paonGrassActor + 0x28, (byte)(ReadMemory(paonGrassActor + 0x28) | 0x80));
                                }
                                break;

                            case 52: //carpenter book
                                if (ReadMemory(0xf870) == 7) //in circus village
                                {
                                    WriteMemory(0x19544, [64, 0, 0, 166, 118, 195, 4, 12, 4, 0, 4, 36, 81, 195, 4, 12], binPtr); //enable statue explosion cutscene
                                }
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
                                break;

                            default:
                                break;
                        }
                    }

                    if (!custom)
                    {
                        if (itemPickedUp[2] == 1) AddItemWithoutMessage(newItemId, 1);
                        else AddItemWithMessage(newItemId, 1);
                    }
                }

                WriteMemory(0xf8b0, [0, 0, 0]);
            }

            if (ReadMemory(0x37e85) == 0 && ReadMemory(0x37ff7) == 1)
            {
                foreach (var item in writeQueueSafe.Where(i => i.DequeueTimeStamp == 0))
                {
                    foreach (var pair in item.AddressValuePairs)
                    {
                        WriteMemory(pair.Address, pair.Value, pair.Ptr, true);
                    }
                    item.DequeueTimeStamp = newIgt;
                }
            }

            var queuedPopups = writeQueuePopup.Where(p => p.DequeueTimeStamp == 0);

            if (queuedPopups.Any() && writeQueuePopup.Max(p => p.DequeueTimeStamp) < newIgt - 60)
            {
                var popup = queuedPopups.First();
                SetCustomPopupString(popup.Text);
                QueueResourceMessage(STRING_CUSTOM, 41);

                popup.DequeueTimeStamp = newIgt;

                WriteMemory(0x250b, 20, globalPtr); //pause conversation script
                conversationScriptPaused = true;
            }

            if (conversationScriptPaused && ReadMemory(0xf550) == 0) //resume conversation script when no popups on screen
            {
                WriteMemory(0x250b, 16, globalPtr);
                conversationScriptPaused = false;
            }

            igt = newIgt;
            IgnoreChanges = false;
            Thread.Sleep(17);
        }

        IsActive = false;
    }

    public void AddItemWithMessage(byte itemId, byte amount, bool custom = false)
    {
        if (!custom)
        {
            AddInventoryQuantity(itemId, amount);
            QueuePopupMessage(itemId, 2, 66);
        }

        else
        {
            var item = randomizer.RandomizedItems.First(r => r.Key.InternalId == itemId).Value;
            AddInventoryQuantity(item.InternalId, amount);
            QueueCustomPopupItem(item);
        }
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

    public void WriteMemory(int address, byte value, IntPtr ptr, bool ignoreRandom = false) => WriteMemory(address, [value], ptr == 0 ? basePtr : ptr);

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

                var enteringInterior = ReadMemory(0xf817, 2);

                if (!interiorTransition)
                {
                    switch (ReadMemory(0xf870)) //current area
                    {
                        case 0:
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
                            break;  

                        case 5:
                            if (enteringInterior[0] == 2 && enteringInterior[1] == 2 && ReadMemory(0xf9c6) == 21) //pham room
                            {
                                WriteMemory(0xf9c6, 22);
                                WriteMemory(0xf8b0, [24, 1, 1]);

                                var item = randomizer.RandomizedItems.First(r => r.Key.InternalId == 24).Value;
                                QueueCustomPopup("{O}Pham{W} gives you " + (item.Color == ItemColor.Green ? "{G}" : item.Color == ItemColor.Blue ? "{B}" : "{P}") + item.DisplayName + "{W}!");
                                
                            }
                            break;

                        case 6:
                            if (enteringInterior[0] == 4) //rock crab room
                            {
                                if (enteringInterior[1] == 2 && ReadMemory(0xfa22) == 48 && ReadMemory(0xf9e2) == 0 && ReadMemory(0xf839) == 0)
                                {
                                    WriteMemory(0xf9e2, 1);

                                    if (ReadMemory(0xfae5) == 1)
                                    {
                                        WriteMemory(0xfa22, 51);
                                        RemoveItemWithMessage(49, 1);
                                        WriteMemory(0xf8b0, [25, 1, 1]);

                                        var item = randomizer.RandomizedItems.First(r => r.Key.InternalId == 25).Value;
                                        QueueCustomPopup("You're given " + (item.Color == ItemColor.Green ? "{G}" : item.Color == ItemColor.Blue ? "{B}" : "{P}") + item.DisplayName + "{W}!");
                                    }
                                    else
                                    {
                                        QueueCustomPopup("Bring a {P}Rock Crab{W} here!");
                                    }
                                }
                                else if (enteringInterior[1] == 3)
                                {
                                    interiorTransition = true;
                                    WriteMemory(0xf9e2, 0);
                                }
                            }
                            break;

                        case 7:
                            if (enteringInterior[0] == 7) //triangle gear interior
                            {
                                if (enteringInterior[1] == 1) //entering
                                {
                                    interiorTransition = true;

                                    if (ReadMemory(0xf8d8) == 1 && ReadMemory(0xfada) == 1) //player has triangle gear but hasn't picked up the object
                                    {
                                        WriteMemory(0xf8d8, 255); //temporarly set gear pickup event to completed 
                                        WriteMemory(0xf9e2, (byte)(ReadMemory(0xf9e2) | 1)); //to revert the event flag when leaving
                                    }
                                    else if (ReadMemory(0xf8d8) == 255 && ReadMemory(0xfada) == 0) //player has picked up object but does not have triangle gear
                                    {
                                        WriteMemory(0xf8d8, 1); 
                                        WriteMemory(0xf9e2, (byte)(ReadMemory(0xf9e2) | 1));
                                    }
                                }
                                else if (enteringInterior[1] == 3) //leaving
                                {
                                    interiorTransition = true;

                                    if ((ReadMemory(0xf9e2) & 1) == 1) WriteMemory(0xf8d8, (byte)(ReadMemory(0xf8d8) == 255 ? 1 : 255)); //flip gear pickup event state
                                }
                            }
                            else if (enteringInterior[0] == 6) //paon interior
                            {
                                if (enteringInterior[1] == 1) //entering
                                {
                                    interiorTransition = true;

                                    if (ReadMemory(0xf8db) == 1 && ReadMemory(0xfae6) != 1) //player has talked to pig elder about the well, lid is not lifted and player does not have paon grass
                                    {
                                        WriteMemory(0x4bca0, 3); //remove pig NPC that starts paon sequence
                                        WriteMemory(0xf9e2, (byte)(ReadMemory(0xf9e2) | 2)); //to revert the event flag when leaving
                                    }
                                }
                                else if (enteringInterior[1] == 3) //leaving
                                {
                                    interiorTransition = true;

                                    if ((ReadMemory(0xf9e2) & 2) == 2) WriteMemory(0x4bca0, 131);
                                }
                            }
                            break;

                        case 8:
                            if (enteringInterior[0] == 2)
                            {
                                if (enteringInterior[1] == 1)
                                {
                                    interiorTransition = true;

                                    if (ReadMemory(0xfac4) == 0)
                                    {
                                        WriteMemory(0x50e08, [57, 79, 85, 251, 78, 69, 69, 68, 251, 65, 251, 244, 48, 73, 71, 251, 51, 85, 73, 84, 240, 1, 255], binPtr); //"You need a Pig Suit!" string
                                        WriteMemory(0x21428, [90, 0, 4, 36, 101, 59, 1, 12, 41, 0, 5, 36], binPtr);
                                        WriteMemory(0x21434, new byte[28], binPtr);
                                        WriteMemory(0x21454, [0, 0, 2, 36], binPtr);
                                    }
                                    else
                                    {
                                        WriteMemory(0x21428, [1, 0, 4, 36, 213, 8, 1, 12, 2, 0, 5, 36, 33, 32, 0, 2, 10, 128, 5, 60, 90, 3, 1, 12, 112, 61, 165, 36, 2, 0, 2, 36, 112, 0, 2, 162, 7, 0, 2, 36, 111, 197, 4, 8, 6, 0, 0, 162], binPtr);
                                    }
                                }
                                else if (enteringInterior[1] == 3 && ReadMemory(0xf9e2) == 1)
                                {
                                    interiorTransition = true;

                                    WriteMemory(0xfa3f, (byte)(ReadMemory(0xfa3f) | 32));
                                    WriteMemory(0xf9e2, 0);
                                }
                            }

                            break;
                    }
                }
                else if (enteringInterior[1] == 2 || enteringInterior[1] == 4) interiorTransition = false;

                var popups = ReadMemory(0xf9f1);

                
                if (popups != 0)
                {
                    if (popups == 255)
                    {
                        var flowersWatered = BitOperations.PopCount(ReadMemory(0xfa13));
                        QueueCustomPopup("Watered " + (flowersWatered == 5 ? "{G}all" : ("{B}" + flowersWatered)) + " {O}Magic Flower" + (flowersWatered > 1 ? "s" : "") + "{W}!");
                    }
                    else
                    {
                        switch (ReadMemory(0xf870)) //current area
                        {
                            case 1:
                                var missingRobeCount = ReadMemory(0xfac6, 5).Count(r => r == 0);
                                if (missingRobeCount > 0) QueueCustomPopup("You are missing {P}" + missingRobeCount + " {G}Evil Pig Robe" + (missingRobeCount > 1 ? "s" : "") +"{W}!");
                                if (ReadMemory(0xfad0) == 0) QueueCustomPopup("You are missing {P}Last Pig Bag{W}!");

                                break;

                            case 2:
                                QueueCustomPopup("You need a {P}Trolley Rail{W}!");
                                break;

                            case 7:
                                QueueCustomPopup("You need a {P}Carpenter Book{W}!");
                                break;

                            case 8:
                                if (ReadMemory(0xf550) == 0) //no active popups
                                {
                                    var cogs = ReadMemory(0xfad8, 3);
                                    var removedCogs = ReadMemory(0xfa4b);
                                    var missingItems = new List<string>();
                                    var blockedItems = new List<string>();

                                    if (cogs[0] == 0 && (removedCogs & 1) == 0) missingItems.Add("Star-shaped Cog");
                                    if (cogs[1] == 0 && (removedCogs & 2) == 0) missingItems.Add("Hexagon Gear");
                                    if (cogs[2] == 0 && (removedCogs & 4) == 0) missingItems.Add("Triangle Gear");

                                    if (missingItems.Any()) QueueCustomPopup("You are missing {P}" + string.Join("{W},{n}{P}", missingItems));

                                    if (cogs[0] == 1 && (removedCogs & 1) == 0) blockedItems.Add("Star-shaped Cog");
                                    if (cogs[1] == 1 && (removedCogs & 2) == 0) blockedItems.Add("Hexagon Gear");
                                    if (cogs[2] == 1 && (removedCogs & 4) == 0) blockedItems.Add("Triangle Gear");

                                    if (blockedItems.Any()) QueueCustomPopup("You cannot hand in {P}" + string.Join("{W},{n}{P}", blockedItems));
                                }
                                break;
                        }
                    }

                    WriteMemory(0xf9f1, 0);
                }
            }
            else
            {
                if (!isWarping && ReadTimer() > 0)
                {
                    warping = false;

                    Thread.Sleep(500);

                    foreach (var item in writeQueueWarp.Where(i => i.DequeueTimeStamp == 0))
                    {
                        foreach (var pair in item.AddressValuePairs)
                        {
                            WriteMemory(pair.Address, pair.Value, pair.Ptr, true);
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

        var temporaryFlags = ReadMemory(0xf9e2);

        if (temporaryFlags != 0)
        {
            switch (ReadMemory(0xf870)) //current area
            {
                case 0:
                    var tempCrabInfo = ReadMemory(0xf9e3);

                    if (tempCrabInfo != 0)
                    {
                        WriteMemory(0xf9e3, (byte)((tempCrabInfo & 0xF0) | (temporaryFlags & 0x0F)));
                    }
                    break;
                case 4:
                    if ((temporaryFlags & 1) == 1) WriteMemory(0xf8cd, 1);
                    if ((temporaryFlags & 2) == 2) WriteMemory(0xf8cd, 255);
                    break;
                case 7:
                    if ((temporaryFlags & 1) == 1) WriteMemory(0xf8d8, 1);
                    if ((temporaryFlags & 2) == 2) WriteMemory(0x4bca0, 131);
                    break;
                case 8:
                    WriteMemory(0xfa3f, (byte)(ReadMemory(0xfa3f) | 32));
                    break;
            }

            WriteMemory(0xf9e2, 0);
        }

        var warpDestination = ReadMemory(0xf83a, 2);

        switch (warpDestination[1])
        {
            case 0:
                if (warpDestination[0] != 0 && warpDestination[0] != 9) //Entering waterfall area of starting beach
                {
                    if (ReadMemory(0xf9dd) < 11)
                    {
                        WriteMemory(0xf9dd, 11); //Can now jump over unraised net bridge
                        WriteMemory(0xf9e5, 2); //Spawn pig holding crab basket and crabs
                    }
                }

                if (ReadMemory(0xfadd) > 0) //player has crab basket in inventory
                {
                    if (ReadMemory(0xf8ba) != 255) //the crab basket event is not completed
                    {
                        WriteMemory(0xf9e5, 2); //spawn basket-holding pig - this also disables crab catching

                        if (ReadMemory(0xf8bb) != 255)
                        {
                            Enqueue(writeQueueWarp, 0xf9e5, [6]); //re-enable crab catching after loading
                            Enqueue(writeQueueWarp, 0x69bc, new byte[4], binPtr); //disable crab catching flag being changed when you hit the basket pig
                        }
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
                else if (ReadMemory(0xf8ba) == 255)
                {
                    WriteMemory(0xf9e5, 7); //prevent crab basket from spawning and disable catching if it has already been picked up
                }

                

                break;

            case 5:
                if (warpDestination[0] == 6) //travelling backwards from donglin
                {
                    if (ReadMemory(0xf8cd) != 255) //static explosion event not completed
                    {
                        var kujaraPurified = (ReadMemory(0xfe56) & 32) == 32;
                        var hasSquirrelClothes = ReadMemory(0xfac3) == 1;

                        if (kujaraPurified || hasSquirrelClothes)
                        {
                            WriteMemory(0x65210, [250, 56, 52, 208, 104, 57, 10, 1], globalPtr); //overwrite warp destination coordinates to put player outside of Pham's hut
                            Enqueue(writeQueueWarp, 0x65210, [0, 43, 192, 208, 0, 78, 10, 43], globalPtr); //revert changes after warp

                            if (!kujaraPurified)
                            {
                                if (ReadMemory(0x37eef) != 15) //auto-equip squirrel clothes if not equipped (maybe add invisibility checks etc)
                                {
                                    Enqueue(writeQueueWarp, 0xf88f, [15]); 
                                    Enqueue(writeQueueWarp, 0x37eef, [15]);
                                    Enqueue(writeQueueWarp, 0xf81d, [1]);
                                    Enqueue(writeQueueWarp, 0x37e84, [4, 17, 0]);
                                }
                            }
                        }
                        else
                        {
                            WriteMemory(0xf83a, 5); //just warp to start of summit if you can't do anything there
                        }
                    }
                }

                break;
        }


        //Prepare pig doors in case player gets the pig bag for that area in that area
        var pigDoorsOpened = ReadMemory(0xfa17);
        var pigDoors = new byte[] { 6, 8, 1, 4, 0 };
        if (pigDoors.Contains(warpDestination[1]) && (pigDoorsOpened & (byte)Math.Pow(2, pigDoors.IndexOf(warpDestination[1]))) == 0)
        {
            var bags = ReadMemory(0xf883, 7);
            if (!bags.Any(b => b == 23 + pigDoors.IndexOf(warpDestination[1]))) //make sure you don't already have the bag
            {
                Enqueue(writeQueueWarp, 0xf883, bags);
                Enqueue(writeQueueWarp, warpDestination[1] == 0 ? 0x4e81d : (warpDestination[1] == 8 ? 0x4e74d : 0x4e26d), [4]);

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

    private void EditBinMemory()
    {
        Thread.Sleep(1000);

        var area = ReadMemory(0xf870);

        SetMagicFlowerPopupLogic(area);

        switch (area)
        {
            case 0:
                WriteMemory(0x7c18, new byte[12], binPtr); //disable pink bucket queue resource message on pickup
                WriteMemory(0x7c30, new byte[28], binPtr); //disable pink bucket auto-equip
                WriteMemory(0x6f34, [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 2, 36, 1, 0, 2, 162, 0, 0, 0, 0, 0, 0, 0, 0], binPtr); //disable attaching crab basket to tomba
                WriteMemory(0xc0bc, new byte[44], binPtr); //disable crab pickup message
                WriteMemory(0x73e4, new byte[4], binPtr); //disable crab catching flag being set on crab basket pickup
                WriteMemory(0x7d0c, [81, 1, 98, 144, 0, 0, 0, 0, 2, 0, 66, 48, 20, 0, 64, 20, 33, 128, 128, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], binPtr); //custom rare fish spawn code
                WriteMemory(0x2a410, [81, 1, 98, 144, 0, 0, 0, 0, 4, 0, 66, 48, 3, 0, 64, 20, 208, 0, 68, 142, 93, 98, 4, 12, 0, 0, 0, 0,
                                                  97, 232, 4, 12, 33, 32, 64, 2, 39, 230, 4, 12, 33, 32, 64, 2, 33, 16, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], binPtr); //custom star-shaped cog spawn code

                if (ReadMemory(0xf9e5) > 0) //disable spawning basket pig when raising bridge if pig has already been spawned/basket has been collected.
                {

                    WriteMemory(0xc954, new byte[96], binPtr);
                    WriteMemory(0xc9c0, new byte[4], binPtr);
                }
                break;
            case 1:
                if (ReadMemory(0xf8bc) != 255) WriteMemory(-0x3ce8, [73, 0], binPtr); //disable travel to starting beach if win's windmill not completed

                if ((ReadMemory(0xfac6, 5).Any(r => r == 0) || ReadMemory(0xfad0) == 0) && ReadMemory(0xf8e5) == 255) //disable unlocking door to ??? if missing an evil pig robe or last pig bag
                {
                    WriteMemory(0xa8b8, [43, 0, 32, 162, 12, 128, 2, 60, 1, 0, 4, 36, 241, 249, 68, 160], binPtr);
                }

                WriteMemory(0x5ac0, new byte[12], binPtr); //remove last evil pig bag resource popup
                break;
            case 2:
                if (ReadMemory(0xf8bf) != 255) WriteMemory(0x25b8, new byte[128], binPtr); //disable travel to pipe area if pull and open not completed

                if (ReadMemory(0xfae0) == 0) //prevent trolley rail cutscene if you don't have the trolley rail
                {
                    WriteMemory(0x10fd8, [12, 128, 3, 60, 1, 0, 2, 36, 241, 249, 98, 160, 0, 0, 2, 36, 5, 0, 98, 162, 0, 0, 0, 0, 0, 0, 0, 0], binPtr);
                }

                break;
            case 3:
                WriteMemory(-0x44c4, [49, 53], binPtr); //custom seeds of strength pickup logic
                break;
            case 4:
                if (ReadMemory(0xf8c6) != 255) WriteMemory(0xf3ec, 3, binPtr); //disable trolley to CMT if deliver to gran not completed

                WriteMemory(0xf8c0, new byte[16], binPtr);
                WriteMemory(0xf8d0, [193, 249, 66, 144, 0, 0, 0, 0, 128, 0, 66, 48], binPtr); //custom big sack spawn code

                WriteMemory(0x6760, [82, 1, 34, 146, 0, 0, 0, 0, 2, 0, 66, 48, 15, 0, 64, 16, 0, 0, 0, 0, 17, 90, 4, 8, 16, 0, 82, 38], binPtr); //custom rucksack spawn code

                WriteMemory(0x21f20, new byte[4], binPtr); //disable auto-equipping big sack

                if ((ReadMemory(0xfe56) & 16) == 16) //purified
                {
                    WriteMemory(0x64c8, 89, binPtr); //prevent three sisters from spawning (not the hide & seek variants)
                    WriteMemory(0x652c, [75, 1], binPtr); //allow pham to spawn if kujaras not fed
                    WriteMemory(0x657c, [75, 1], binPtr); //stop traveller from spawning if kujaras not fed

                    if (ReadMemory(0xf8cb) == 1) WriteMemory(0xf7af, 16, binPtr); //fix mountain peach not spawning more than once
                }
                break;
            case 5:
                if (ReadMemory(0xf8ca) != 255) WriteMemory(0x19e8c, 3, binPtr); //disable lift to ranch if let's take the lift not completed
                if ((ReadMemory(0xfe56) & 32) == 32) //purified
                {
                    WriteMemory(0x30eb0, 38, binPtr); //change hexagon gear pickup sprite 
                    if (ReadMemory(0xf937) != 0 && (ReadMemory(0xfa4b) & 2) == 0) //allowed to recollect gears and hexagon gear has not been recollected
                    {
                        if ((ReadMemory(0xf9c2) & 1) == 1) //hexagon gear has not been obtained yet
                        {
                            WriteMemory(0x182be, [0, 0], binPtr); //disable recollecting hexagon gear
                        }
                    }
                }
                break;
            case 6:
                WriteMemory(0xd600, [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 81, 1, 130, 144, 0, 0, 0, 0, 16, 0, 66, 48], binPtr); //custom blue fruit spawn code
                WriteMemory(0x58a0, [81, 1, 34, 146, 0, 0, 0, 0, 64, 0, 66, 48, 68, 0, 64, 20], binPtr); //custom rock crab spawn code
                WriteMemory(0x50e54, [255, 52, 72, 69, 251, 243, 44, 73, 71, 72, 84, 13, 35, 85, 84, 84, 73], binPtr); //repair potential damage to a resource string

                if ((ReadMemory(0xf9c1) & 64) == 0) WriteMemory(0x58a0, [75, 86, 4, 8], binPtr); //rock crab spawn
                if ((ReadMemory(0xf9c1) & 32) == 32) WriteMemory(0x11e3c, 2); //disable clear fruit pickup
                if (ReadMemory(0xf8cd) != 255) WriteMemory(0x14074, 3, binPtr); //disable lift to summit if static explosion not completed
                break;
            case 7:
                if (ReadMemory(0xf8d5) != 255) WriteMemory(-0x5750, [73, 0], binPtr); //disable travel to deep forest if use rock crabs for balance not completed
                WriteMemory(0x1687c, new byte[4], binPtr); //disable losing paon grass on failure
                WriteMemory(0x1689c, new byte[36], binPtr); //disable losing paon grass on failure
                WriteMemory(0x192f4, 100, binPtr); //prevent gaining duplicate items from elder pig (paon grass)

                if (ReadMemory(0xf8d8) != 255 && (ReadMemory(0xf9d6) == 14)) //allow ball to spawn even if bridge has been raised
                {
                    WriteMemory(0x3a14, 30, binPtr);
                    WriteMemory(0x1ad2c, 30, binPtr);
                    WriteMemory(0x1ba80, 14, binPtr);
                    WriteMemory(0xfa45, (byte)(ReadMemory(0xfa45) & ~(1 << 0)));
                    WriteMemory(0xf9e2, (byte)(ReadMemory(0xf9e2) | 4));
                }

                WriteMemory(0x3a2C, 255, binPtr); //spawn circus ball if triangle gear pickup not grabbed but bridge has been raised

                if (ReadMemory(0xfae8) == 0 && ReadMemory(0xf8dc) != 255) //prevent statue explosion cutscene if player doesn't have the carpenter book
                {
                    WriteMemory(0x19544, [12, 128, 3, 60, 1, 0, 2, 36, 165, 165, 4, 8, 241, 249, 98, 160], binPtr);
                }

                if (ReadMemory(0xf8d8) != 255 & (ReadMemory(0xfe56) & 128) == 128) //allow moveable ball to spawn if circus has been purified
                {
                    WriteMemory(0x12da4, new byte[8], binPtr);
                    WriteMemory(0x12dac, 2, binPtr);
                }
                break;
            case 8:
                WriteMemory(0x4f28, [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 81, 1, 2, 146, 0, 0, 0, 0, 8, 0, 66, 48, 181, 0, 64, 16, 0, 0, 0, 0], binPtr);

                WriteMemory(0x182e0, [38, 249, 67, 144, 255, 0, 2, 36, 53, 2, 98, 16], binPtr); //custom taboo fruit spawn code

                if (ReadMemory(0xf8dc) != 255) WriteMemory(-0x5354, new byte[28], binPtr); //disable travel to circus village if a pig tribe clown statue not completed

                if ((ReadMemory(0xfa44) & 2) == 0 && ReadMemory(0xf8e4) == 255) //spawn water pig bag pickup if hasn't been collected yet but water pig has been defeated
                {
                    WriteMemory(0x51cb, 20, binPtr);
                    WriteMemory(0x1309B, 20, binPtr);
                }

                if ((ReadMemory(0xfa3f) & 32) == 0) //prevent first giant fish cutscene if mermaid has not given you the swimming pig suit
                {
                    WriteMemory(0x2d240, 255, binPtr);
                }

                var removedCogs = ReadMemory(0xfa4b);

                if (ReadMemory(0xf937) == 1 && removedCogs != 7 && (ReadMemory(0xfa46) & 15) != 15) //custom minitta tunnel gear giving logic
                {
                    WriteMemory(0x294b8, [12, 128, 2, 60, 1, 0, 4, 36, 241, 249, 68, 160, 8, 0, 224, 3, 43, 0, 0, 162], binPtr);
                }                
                
                break;
            default:
                break;
        }
    }

    private void SetMagicFlowerPopupLogic(byte area)
    {
        var address = 0;
        switch (area)
        {
            case 0: address = 0x2b0a4; break;
            case 1: address = 0x1c680; break;
            case 5: address = 0x1bd68; break;
            case 7: address = 0xbdec; break;
            case 8: address = 0x103b8; break;
            default: return;
        }

        WriteMemory(address, [12, 128, 6, 60, 112, 248, 198, 36, 3, 0, 2, 146, 163, 1, 195, 144, 4, 16, 81, 0, 37, 24, 98, 0, 163, 1, 195, 160, 12, 128, 3, 60, 255, 0, 2, 36, 241, 249, 98, 160], binPtr);
    }

    private void CompleteEvent(byte id, bool loud = true)
    {
        if (ReadMemory(0x37fee) != 0) //check if tomba alive
        {
            var ptrEventState = 0xf8b4 + id;
            var eventState = ReadMemory(ptrEventState);

            if (eventState == 0) WriteMemory(0xf8a8, (byte)(ReadMemory(0xf8a8) + 1)); //increase started events by 1
            if (eventState != 255)
            {
                WriteMemory(ptrEventState, 255);
                WriteMemory(0xf8aa, (byte)(ReadMemory(0xf8aa) + 1)); //increase completed events by 1
                var eventAPReward = GetEventAPReward(id, true);

                var currentAP = BitConverter.ToInt32(ReadMemory(0xf874, 4)); //update AP
                WriteMemory(0xf874, BitConverter.GetBytes(currentAP + eventAPReward));

                if (loud)
                {
                    var pendingEventNotificationCount = ReadMemory(0x3d06d);
                    WriteMemory(0x3d06e + pendingEventNotificationCount, id); //event popup
                    WriteMemory(0x3d074 + pendingEventNotificationCount, 1);
                    WriteMemory(0x3d06d, (byte)(pendingEventNotificationCount + 1));
                }
                else
                {
                    QueueCustomPopupEvent(id);
                }
            }
        }
    }

    private void QueueCustomPopupEvent(byte id) => QueueCustomPopup("{O}" + randomizer.Events[id].Name + "{W} completed!");

    private void QueueCustomPopupItem(Item item, byte type = 0)
    {
        var end = "";
        if (type == 0) end = "acquired!";
        else if (type == 1) end = "found!";

        QueueCustomPopup((item.Color == ItemColor.Green ? "{G}" : (item.Color == ItemColor.Blue ? "{B}" : "{P}")) + item.DisplayName + "{W} " + end);
    }

    private void QueueCustomPopup(string input) => writeQueuePopup.Add(new QueuedChange(igt, input));

    private void SetCustomPopupString(string input)
    {
        var popupText = new byte[input.Length - input.Count('{') * 2 + 1];
        int outputIndex = 0;

        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] >= '!' && input[i] <= 'z')
            {
                popupText[outputIndex] = (byte)(input[i] - 32);
            }
            else if (input[i] == ' ') popupText[outputIndex] = 251;
            else if (input[i] == '{')
            {
                switch (input[i + 1])
                {
                    case 'n':
                        popupText[outputIndex] = 250; //newline
                        break;
                    case 'O':
                        popupText[outputIndex] = 241; //orange
                        break;
                    case 'B':
                        popupText[outputIndex] = 242; //blue
                        break;
                    case 'P':
                        popupText[outputIndex] = 243; //pink
                        break;
                    case 'G':
                        popupText[outputIndex] = 244; //green
                        break;
                    case 'W':
                        popupText[outputIndex] = 240; //end colour
                        break;
                }
                i += 2;
            }
            outputIndex++;
        }

        popupText[outputIndex] = 255;

        WriteMemory(0x50e08, popupText, binPtr);

    }

    private int AllocateActorPool1()
    {
        var poolHead = GetAddressPointer(0x380a0);
        var uVar5 = GetAddressPointer((int)(poolHead - 0x800b0000 + 0x24));
        WriteMemory(0x37e7d, (byte)(ReadMemory(0x37e7d) - 1));

        var piVar4 = 0x4239c;
        var piVar6 = 0x42624;

        var iVar2 = GetAddressPointer(piVar4);
        var puVar1 = poolHead - 0x800b0000 + 0x24;
        WriteMemory(0x380a0, BitConverter.GetBytes(uVar5));
        WriteMemory((int)puVar1, [0, 0, 0, 0]);
        WriteMemory((int)(poolHead - 0x800b0000 + 0x20), BitConverter.GetBytes(iVar2));
        if (ReadMemory((int)(GetAddressPointer(piVar4) - 0x800b0000)) == 0)
        {
            WriteMemory((int)(GetAddressPointer(piVar6) - 0x800b0000), BitConverter.GetBytes(poolHead));
        }
        else
        {
            WriteMemory((int)(GetAddressPointer(piVar4) - 0x800b0000 + 0x24), BitConverter.GetBytes(poolHead));
        }

        WriteMemory(piVar4, BitConverter.GetBytes(poolHead));

        WriteMemory((int)(poolHead - 0x800b0000 + 0xa), 1);
        WriteMemory((int)(poolHead - 0x800b0000), 2);
        WriteMemory((int)(poolHead - 0x800b0000 + 0xc), 3);

        return (int)(poolHead - 0x800b0000);
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
            case 6:
                if (id == 23 || id == 25 || id == 26) WriteMemory(0x4e26d, 2);
                break;
            case 8:
                if (id == 24) WriteMemory(0x4e74d, 2);
                break;
        }
    }

    private uint GetAddressPointer(int address) => BitConverter.ToUInt32(ReadMemory(address, 4));

    private int ReadDialogueOffsetPtr() => BitConverter.ToUInt16(ReadMemory(0x4240c, 2)) + 0x4006C;


    private void SetFlagRareFish() => WriteMemory(0xf9c1, (byte)(ReadMemory(0xf9c1) | 0b_0000_0010));
    private void SetFlagStarShapedCog() => WriteMemory(0xf9c1, (byte)(ReadMemory(0xf9c1) | 0b_0000_0100));
    private void SetFlagRoundCog() => WriteMemory(0xf9c1, (byte)(ReadMemory(0xf9c1) | 0b_0000_1000));
    private void SetFlagBlueFruit() => WriteMemory(0xf9c1, (byte)(ReadMemory(0xf9c1) | 0b_0001_0000));
    private void SetFlagClearFuit() => WriteMemory(0xf9c1, (byte)(ReadMemory(0xf9c1) | 0b_0010_0000));
    private void SetFlagRockCrab() => WriteMemory(0xf9c1, (byte)(ReadMemory(0xf9c1) | 0b_0100_0000));
    private void SetFlagBigSack() => WriteMemory(0xf9c1, (byte)(ReadMemory(0xf9c1) | 0b_1000_0000));

    private void SetFlagRemoveHexagonGear() => WriteMemory(0xf9c2, (byte)(ReadMemory(0xf9c2) | 0b_0000_0001));
    private void SetFlagRucksack() => WriteMemory(0xf9c2, (byte)(ReadMemory(0xf9c2) | 0b_0000_0010));
    private void SetFlagSnowFireflyBox() => WriteMemory(0xf9c2, (byte)(ReadMemory(0xf9c2) | 0b_0000_0100));

    private void HandleRewind(int time)
    {
        writeQueueSafe.RemoveAll(i => i.EnqueueTimeStamp > time);
        writeQueueWarp.RemoveAll(i => i.EnqueueTimeStamp > time);
        writeQueuePopup.RemoveAll(i => i.EnqueueTimeStamp > time);

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

        foreach (var item in writeQueuePopup.Where(i => i.DequeueTimeStamp != 0))
        {
            if (time < item.DequeueTimeStamp)
            {
                item.DequeueTimeStamp = 0;
            }
        }

        WriteMemory(0x250b, 16, globalPtr); //make sure conversation script doesnt get stuck
    }

    public void Enqueue(List<QueuedChange> queue, List<AddressValuePair> pairs) => queue.Add(new QueuedChange(ReadTimer(), pairs));
    public void Enqueue(List<QueuedChange> queue, AddressValuePair pair) => Enqueue(queue, new List<AddressValuePair> { pair });

    public void Enqueue(List<QueuedChange> queue, int address, byte[] values, IntPtr ptr)
    {
        var pairs = new List<AddressValuePair>();
        for (int i = 0; i < values.Length; i++)
        {
            pairs.Add(new AddressValuePair { Address = address + i, Value = values[i], Ptr = ptr });
        }

        Enqueue(queue, pairs);
    }

    public void Enqueue(List<QueuedChange> queue, int address, byte[] values) => Enqueue(queue, address, values, basePtr);

    private void ClearQueueHistories()
    {
        while (ProcessIsActive)
        {
            writeQueueSafe.RemoveAll(i => i.DequeueTimeStamp != 0 && igt - i.DequeueTimeStamp > 50000);
            writeQueueWarp.RemoveAll(i => i.DequeueTimeStamp != 0 && igt - i.DequeueTimeStamp > 50000);
            writeQueuePopup.RemoveAll(i => i.DequeueTimeStamp != 0 && igt - i.DequeueTimeStamp > 50000);

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