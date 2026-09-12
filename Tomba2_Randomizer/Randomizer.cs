using System;
using System.Collections.Generic;
using System.Linq;

namespace Tomba2_Randomizer
{
    public class Randomizer
    {
        private Dictionary<int, Item> items;
        private Dictionary<int, Area> areas;
        private Dictionary<int, Event> events;

        private Dictionary<int, Item> allItems;
        private Dictionary<int, Item> notRandomItems;

        Random r = new();

        private bool success;

        public Randomizer(List<ItemDto> itemDtos, List<AreaDto> areaDtos, List<EventDto> eventDtos)
        {
            allItems = itemDtos.ToDictionary(
                dto => dto.Id,
                dto => new Item { Id = (byte)dto.Id, Name = dto.Name, DisplayName = dto.GUIName, CountAddress = Convert.ToInt32(dto.Address, 16), Color = dto.Color == "Green" ? ItemColor.Green : dto.Color == "Blue" ? ItemColor.Blue : ItemColor.Pink, NotRandom = dto.NotRandom }
            );

            items = allItems.Where(i => !i.Value.NotRandom).ToDictionary();
            notRandomItems = allItems.Where(i => i.Value.NotRandom).ToDictionary();

            areas = areaDtos.ToDictionary(
                dto => dto.Id,
                dto => new Area { Id = dto.Id, Name = dto.Name }
            );

            events = eventDtos.ToDictionary(
                dto => dto.Id,
                dto => new Event { Address = Convert.ToInt32(dto.Address, 16), Name = dto.Name, AP = dto.AP }
            );

            foreach (var dto in itemDtos.Where(i => !i.NotRandom))
            {
                var item = items[dto.Id];

                foreach (var reqDto in dto.Requirements ?? [])
                {
                    var group = new RequirementGroup();

                    if (reqDto.Items != null)
                    {
                        group.Items.AddRange(reqDto.Items
                            .Where(items.ContainsKey)
                            .Select(id => items[id]));
                    }

                    if (reqDto.Areas != null)
                    {
                        group.Areas.AddRange(reqDto.Areas
                            .Where(areas.ContainsKey)
                            .Select(id => areas[id]));
                    }

                    if (reqDto.Events != null)
                    {
                        group.Events.AddRange(reqDto.Events
                            .Where(events.ContainsKey)
                            .Select(id => events[id]));
                    }

                    group.AP = reqDto.AP;

                    item.RequirementGroups.Add(group);
                }
            }

            foreach (var dto in areaDtos)
            {
                var area = areas[dto.Id];

                foreach (var reqDto in dto.Requirements ?? [])
                {
                    var group = new RequirementGroup();

                    if (reqDto.Items != null)
                    {
                        group.Items.AddRange(reqDto.Items
                            .Where(items.ContainsKey)
                            .Select(id => items[id]));
                    }

                    if (reqDto.Areas != null)
                    {
                        group.Areas.AddRange(reqDto.Areas
                            .Where(areas.ContainsKey)
                            .Select(id => areas[id]));
                    }

                    if (reqDto.Events != null)
                    {
                        group.Events.AddRange(reqDto.Events
                            .Where(events.ContainsKey)
                            .Select(id => events[id]));
                    }

                    group.AP = reqDto.AP;

                    area.RequirementGroups.Add(group);
                }
            }

            foreach (var dto in eventDtos)
            {
                var eventvar = events[dto.Id];

                foreach (var reqDto in dto.Requirements ?? [])
                {
                    var group = new RequirementGroup();

                    if (reqDto.Items != null)
                    {
                        group.Items.AddRange(reqDto.Items
                            .Where(items.ContainsKey)
                            .Select(id => items[id]));
                    }

                    if (reqDto.Areas != null)
                    {
                        group.Areas.AddRange(reqDto.Areas
                            .Where(areas.ContainsKey)
                            .Select(id => areas[id]));
                    }

                    if (reqDto.Events != null)
                    {
                        group.Events.AddRange(reqDto.Events
                            .Where(events.ContainsKey)
                            .Select(id => events[id]));
                    }

                    group.AP = reqDto.AP;

                    eventvar.RequirementGroups.Add(group);
                }
            }
        }

        private List<Item> RandomItemPool
        {
            get
            {
                return items.Values.Except(RandomizedItems.Values).ToList();
            }
        }

        private IEnumerable<Item> AvailableItems
        {
            get
            {
                return items.Values.Where(i => i.Unlocked);
            }
        }

        private List<Item> AvailableItemsToRandomize
        {
            get
            {
                return AvailableItems.Except(RandomizedItems.Keys).ToList();
            }
        }

        public string DebugString { get; set; }

        public Dictionary<Item, Item> RandomizedItems { get; set; }

        public Dictionary<byte, Event> Events
        {
            get
            {
                return events.Values.ToDictionary(
                    e => e.Id,
                    e => e
                );
            }
        }


        public void Randomize()
        {
            while (!success)
            {
                RandomizedItems = [];
                UpdateItems();

                while (RandomItemPool.Count > 0)
                {
                    if (AvailableItemsToRandomize.Count == 0)
                    {
                        //DebugString += $"\nRan out of available items. {RandomItemPool.Count} items left\n";
                        break;
                    }
                    else if (AvailableItemsToRandomize.Count <= 2 && RandomItemPool.Count > 1)
                    {
                        var itemToRandomize = AvailableItemsToRandomize[0];

                        var potentialUnlocks = RandomItemPool
                            .Select(reward => new
                            {
                                Item = reward,
                                Unlocks = GetHypotheticalUnlocks(itemToRandomize, reward)
                            })
                            .Where(i => i.Unlocks.Count > 0)
                            .ToList();

                        if (potentialUnlocks.Count > 0)
                        {
                            var choice = potentialUnlocks[r.Next(potentialUnlocks.Count)];
                            RandomizedItems[itemToRandomize] = choice.Item;
                        }
                        else
                        {
                            RandomizedItems[AvailableItemsToRandomize[r.Next(AvailableItemsToRandomize.Count())]] = RandomItemPool[r.Next(RandomItemPool.Count())];
                        }
                    }
                    else
                    {
                        RandomizedItems[AvailableItemsToRandomize[r.Next(AvailableItemsToRandomize.Count())]] = RandomItemPool[r.Next(RandomItemPool.Count())];
                    }
                    UpdateItems();
                }

                if (RandomItemPool.Count == 0)
                {
                    success = true;

                    foreach (var item in notRandomItems.Values)
                    {
                        RandomizedItems[item] = item;
                    }

                    foreach (var pair in RandomizedItems.Where(i => !i.Key.NotRandom))
                    {
                        DebugString += $"{pair.Key.Name} gives {pair.Value.Name}\n";
                    }
                }
            }            
        }


        public void Randomize(string itemString)
        {
            RandomizedItems = new Dictionary<Item, Item>();
            foreach (var line in itemString.Split('|'))
            {
                var splitLine = line.Split(",");
                RandomizedItems[allItems.Values.First(i => i.Id == Convert.ToInt32(splitLine[0]))] = allItems.Values.First(i => i.Id == Convert.ToInt32(splitLine[1]));
            }
        }

        private void UpdateItems()
        {
            var unlockedItems = new HashSet<Item>();
            var unlockedEvents = new HashSet<Event>();
            var unlockedAreas = new HashSet<Area>();

            CalculateUnlockState(unlockedItems, unlockedEvents, unlockedAreas, RandomizedItems);

            foreach (var item in items.Values) item.Unlocked = unlockedItems.Contains(item);

            foreach (var ev in events.Values) ev.Unlocked = unlockedEvents.Contains(ev);

            foreach (var area in areas.Values) area.Unlocked = unlockedAreas.Contains(area);
        }

        private void CalculateUnlockState(HashSet<Item> unlockedItems, HashSet<Event> unlockedEvents, HashSet<Area> unlockedAreas, IReadOnlyDictionary<Item, Item> randomizedItems)
        {
            bool changed;

            do
            {
                changed = false;

                var obtainedItems = unlockedItems.Where(randomizedItems.ContainsKey).Select(item => randomizedItems[item]).ToHashSet();

                int availableAP = unlockedEvents.Sum(e => e.AP);

                foreach (var item in items.Values)
                {
                    if (unlockedItems.Contains(item)) continue;

                    bool canUnlock = item.RequirementGroups.Count == 0 || item.RequirementGroups.Any(rg => rg.Items.All(requiredItem => obtainedItems.Contains(requiredItem)) &&
                            rg.Events.All(e => unlockedEvents.Contains(e)) && rg.Areas.All(a => unlockedAreas.Contains(a)) && availableAP >= rg.AP);

                    if (canUnlock)
                    {
                        unlockedItems.Add(item);
                        changed = true;
                    }
                }

                foreach (var ev in events.Values)
                {
                    if (unlockedEvents.Contains(ev)) continue;

                    bool canUnlock = ev.RequirementGroups.Count == 0 || ev.RequirementGroups.Any(rg => rg.Items.All(requiredItem => obtainedItems.Contains(requiredItem)) &&
                            rg.Events.All(e => unlockedEvents.Contains(e)) && rg.Areas.All(a => unlockedAreas.Contains(a)) && availableAP >= rg.AP);

                    if (canUnlock)
                    {
                        unlockedEvents.Add(ev);
                        changed = true;
                    }
                }

                foreach (var area in areas.Values)
                {
                    if (unlockedAreas.Contains(area)) continue;

                    bool canUnlock = area.RequirementGroups.Count == 0 || area.RequirementGroups.Any(rg => rg.Items.All(requiredItem => obtainedItems.Contains(requiredItem)) &&
                            rg.Events.All(e => unlockedEvents.Contains(e)) && rg.Areas.All(a => unlockedAreas.Contains(a)));

                    if (canUnlock)
                    {
                        unlockedAreas.Add(area);
                        changed = true;
                    }
                }

            } while (changed);
        }

        private HashSet<Item> GetHypotheticalUnlocks(Item pickup, Item reward)
        {
            var initiallyUnlockedItems = items.Values.Where(i => i.Unlocked).ToHashSet();

            var unlockedItems = initiallyUnlockedItems.ToHashSet();
            var unlockedEvents = events.Values.Where(e => e.Unlocked).ToHashSet();
            var unlockedAreas = areas.Values.Where(a => a.Unlocked).ToHashSet();

            var hypotheticalRandomizedItems =
                new Dictionary<Item, Item>(RandomizedItems)
                {
                    [pickup] = reward
                };

            CalculateUnlockState(unlockedItems, unlockedEvents, unlockedAreas, hypotheticalRandomizedItems);

            return unlockedItems.Except(initiallyUnlockedItems).ToHashSet();
        }
    }
}