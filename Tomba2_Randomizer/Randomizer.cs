using System;
using System.Collections.Generic;
using System.Linq;

namespace Tomba2_Randomizer
{
    public class Randomizer
    {
        private Dictionary<int, Area> areas;
        private Dictionary<int, Event> events;

        private string debugQueue;
        
        Random r = new();

        public Randomizer(List<ItemDto> itemDtos, List<AreaDto> areaDtos, List<EventDto> eventDtos)
        {
            Items = itemDtos.ToDictionary(
                dto => dto.Id,
                dto => new Item { Id = dto.Id, Name = dto.Name, DisplayName = dto.GUIName, CountAddress = Convert.ToInt32(dto.Address, 16), Color = dto.Color == "Green" ? ItemColor.Green : dto.Color == "Blue" ? ItemColor.Blue : ItemColor.Pink}
            );

            areas = areaDtos.ToDictionary(
                dto => dto.Id,
                dto => new Area { Id = dto.Id, Name = dto.Name }
            );

            events = eventDtos.ToDictionary(
                dto => dto.Id,
                dto => new Event { Id = dto.Id, Name = dto.Name, AP = dto.AP }
            );

            foreach (var dto in itemDtos)
            {
                var item = Items[dto.Id];

                foreach (var reqDto in dto.Requirements ?? [])
                {
                    var group = new RequirementGroup();

                    if (reqDto.Items != null)
                    {
                        group.Items.AddRange(reqDto.Items
                            .Where(Items.ContainsKey)
                            .Select(id => Items[id]));
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
                            .Where(Items.ContainsKey)
                            .Select(id => Items[id]));
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
                            .Where(Items.ContainsKey)
                            .Select(id => Items[id]));
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

        private IEnumerable<Item> ItemsToRandomize
        {
            get
            {
                return Items.Values.Except(RandomizedItems.Values);
            }
        }

        private IEnumerable<Item> AvailableItems
        {
            get
            {
                return Items.Values.Where(i => !i.RequirementGroups.Any() || i.RequirementGroups.Any(rg => !rg.Items.Except(RandomizedItems.Values).Any() && rg.Areas.All(area => area.Unlocked) && rg.Events.All(e => e.Unlocked) && events.Values.Where(e => e.Unlocked).Sum(e => e.AP) > rg.AP));
            }
        }

        public Dictionary<int, Item> Items { get; private set; }

        public string DebugString { get; set; }

        public Dictionary<Item, Item> RandomizedItems { get; set; }

        public void Randomize()
        {
            DebugString = "---GAME START---\n";
            debugQueue = "";
            RandomizedItems = new Dictionary<Item, Item>();
            UpdateEventsAndAreas(true);
            DebugString += $"{debugQueue}\n";
            debugQueue = "";

            while (ItemsToRandomize.Any())
            {
                var randomItemPool = Items.Values.Except(RandomizedItems.Values);
                var availableItemPool = AvailableItems.Except(RandomizedItems.Keys);

                if (availableItemPool.Any())
                {
                    var randomItem = randomItemPool.ElementAt(r.Next(randomItemPool.Count()));
                    var randomAvailableItem = availableItemPool.ElementAt(r.Next(availableItemPool.Count()));

                    RandomizedItems[randomAvailableItem] = randomItem;

                    UpdateEventsAndAreas(true);

                    if (AvailableItems.Except(RandomizedItems.Keys).Count() > availableItemPool.Count() + 1)
                    {
                        RandomizedItems[randomAvailableItem].Important = true;
                        DebugString += $"{randomAvailableItem.Name} gives {RandomizedItems[randomAvailableItem].Name} [IMPORTANT]\n";
                    }
                    else
                    {
                        DebugString += $"{randomAvailableItem.Name} gives {RandomizedItems[randomAvailableItem].Name}\n";
                    }

                    if (!string.IsNullOrEmpty(debugQueue)) DebugString += $"{debugQueue}\n";
                    debugQueue = "";
                }
                else
                {
                    var currentAvailableItems = AvailableItems.ToList();

                    bool replaced = false;
                    
                    var deadKeys = new List<Item>();

                    while (!replaced)
                    {
                        bool noPossibleUnlocks = false;

                        var keyPool = RandomizedItems.Keys.Where(k => !k.Important).Except(deadKeys);

                        if (!keyPool.Any())
                        {
                            keyPool = RandomizedItems.Keys;
                            noPossibleUnlocks = true;
                        }

                        var randomKey = keyPool.ElementAt(r.Next(keyPool.Count()));

                        var item = RandomizedItems[randomKey];

                        var newPool = new List<Item>(randomItemPool);

                        while (newPool.Any() && !replaced)
                        {
                            var backupItem = RandomizedItems[randomKey];

                            var randomItem = newPool.ElementAt(r.Next(newPool.Count()));
                            RandomizedItems[randomKey] = randomItem;

                            debugQueue = "";

                            UpdateEventsAndAreas(false);
                            UpdateEventsAndAreas(true);

                            if ((!currentAvailableItems.Except(AvailableItems).Any() && (AvailableItems.Count() > currentAvailableItems.Count())) || noPossibleUnlocks)
                            {
                                RandomizedItems[randomKey].Important = true;
                                replaced = true;
                                DebugString += $"\n{randomKey.Name} gave {item.Name}, now it gives {RandomizedItems[randomKey].Name} [IMPORTANT]\n";
                                if (!string.IsNullOrEmpty(debugQueue)) DebugString += $"{debugQueue}\n";
                                debugQueue = "";
                            }
                            else
                            {
                                newPool.Remove(randomItem);
                                RandomizedItems[randomKey] = backupItem;

                                UpdateEventsAndAreas(false);
                                UpdateEventsAndAreas(true);

                                debugQueue = "";
                            }
                        }

                        if (!replaced)
                        {
                            deadKeys.Add(randomKey);
                        }
                    }
                    
                }
            }

            if (!string.IsNullOrEmpty(debugQueue)) DebugString += $"{debugQueue}\n";
        }

        public void Randomize(string itemString)
        {
            RandomizedItems = new Dictionary<Item, Item>();
            foreach (var line in itemString.Split('|'))
            {
                var splitLine = line.Split(",");
                RandomizedItems[Items.Values.First(i => i.Id == Convert.ToInt32(splitLine[0]))] = Items.Values.First(i => i.Id == Convert.ToInt32(splitLine[1]));
            }
        }

        private void UpdateEventsAndAreas(bool unlock)
        {
            var eventCount = events.Values.Count(e => e.Unlocked != unlock);
            var areaCount = areas.Values.Count(a => a.Unlocked != unlock);

            foreach (var ev in events.Values.Where(e => e.Unlocked != unlock))
            {
                var isUnlocked = !ev.RequirementGroups.Any() || ev.RequirementGroups.Any(rg => !rg.Items.Except(RandomizedItems.Values).Any() && rg.Events.All(e => e.Unlocked) && rg.Areas.All(a => a.Unlocked) && events.Values.Where(e => e.Unlocked).Sum(e => e.AP) >= rg.AP);
                ev.Unlocked = isUnlocked;

                if (unlock && isUnlocked) debugQueue += $"EVENT {ev.Name} Unlocked\n";
                else if (!unlock && !isUnlocked) debugQueue += $"EVENT {ev.Name} Relocked\n";
            }

            foreach (var area in areas.Values.Where(a => a.Unlocked != unlock))
            {
                var isUnlocked = !area.RequirementGroups.Any() || area.RequirementGroups.Any(rg => !rg.Items.Except(RandomizedItems.Values).Any() && rg.Events.All(e => e.Unlocked) && rg.Areas.All(a => a.Unlocked));
                area.Unlocked = isUnlocked;

                if (unlock && isUnlocked) debugQueue += $"AREA {area.Name} Unlocked\n";
                else if (!unlock && !isUnlocked) debugQueue += $"AREA {area.Name} Relocked\n";
            }

            if (eventCount != events.Values.Count(e => e.Unlocked != unlock) || areaCount != areas.Values.Count(a => a.Unlocked != unlock)) UpdateEventsAndAreas(unlock);
        }
    }
}