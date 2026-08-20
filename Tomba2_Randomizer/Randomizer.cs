using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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

        private string debugQueue;
        
        Random r = new();

        public Randomizer(List<ItemDto> itemDtos, List<AreaDto> areaDtos, List<EventDto> eventDtos)
        {
            allItems = itemDtos.ToDictionary(
                dto => dto.Id,
                dto => new Item { Id = dto.Id, Name = dto.Name, DisplayName = dto.GUIName, CountAddress = Convert.ToInt32(dto.Address, 16), Color = dto.Color == "Green" ? ItemColor.Green : dto.Color == "Blue" ? ItemColor.Blue : ItemColor.Pink, NotRandom = dto.NotRandom }
            );

            items = allItems.Where(i => !i.Value.NotRandom).ToDictionary();
            notRandomItems = allItems.Where(i => i.Value.NotRandom).ToDictionary();

            areas = areaDtos.ToDictionary(
                dto => dto.Id,
                dto => new Area { Id = dto.Id, Name = dto.Name }
            );

            events = eventDtos.ToDictionary(
                dto => dto.Id,
                dto => new Event { Id = dto.Id, Name = dto.Name, AP = dto.AP }
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

        private IEnumerable<Item> ItemsToRandomize
        {
            get
            {
                return items.Values.Except(RandomizedItems.Values);
            }
        }

        private IEnumerable<Item> AvailableItems
        {
            get
            {
                return items.Values.Where(i => !i.RequirementGroups.Any() || i.RequirementGroups.Any(rg => !rg.Items.Except(RandomizedItems.Values).Any() && rg.Areas.All(area => area.Unlocked) && rg.Events.All(e => e.Unlocked) && events.Values.Where(e => e.Unlocked).Sum(e => e.AP) > rg.AP));
            }
        }

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
                var randomItemPool = items.Values.Except(RandomizedItems.Values).ToList();
                var availableItemPool = AvailableItems.Except(RandomizedItems.Keys);

                if (availableItemPool.Any())
                {
                    var randomItem = randomItemPool.ElementAt(r.Next(randomItemPool.Count()));
                    var randomAvailableItem = availableItemPool.ElementAt(r.Next(availableItemPool.Count()));

                    RandomizedItems[randomAvailableItem] = randomItem;

                    foreach (var group in randomAvailableItem.RequirementGroups)
                    {
                        if (!group.Items.Except(RandomizedItems.Values).Any() && group.Areas.All(a => a.Unlocked) && group.Events.All(e => e.Unlocked))
                        {
                            randomAvailableItem.ImportantGroups.Add(group);
                        }
                    }

                    UpdateEventsAndAreas(true);

                    debugQueue = "";
                }
                else
                {
                    var keyPool = RandomizedItems.Keys.Where(k => !RandomizedItems.Keys.Any(i => i.ImportantGroups.Any(g => g.Items.Contains(RandomizedItems[k]))) && !events.Values.Any(e => e.Unlocked && e.ImportantGroups.Any(g => g.Items.Contains(RandomizedItems[k])) && !areas.Values.Any(a => a.Unlocked && a.ImportantGroups.Any(g => g.Items.Contains(RandomizedItems[k]))))).ToList();

                    var randomKey = keyPool.ElementAt(r.Next(keyPool.Count()));
                    randomKey.ImportantGroups = [];

                    foreach (var group in randomKey.RequirementGroups)
                    {
                        if (!group.Items.Except(RandomizedItems.Values).Any() && group.Areas.All(a => a.Unlocked) && group.Events.All(e => e.Unlocked))
                        {
                            randomKey.ImportantGroups.Add(group);
                        }
                    }

                    RandomizedItems[randomKey] = randomItemPool.ElementAt(r.Next(randomItemPool.Count()));
                            
                    UpdateEventsAndAreas(false);
                    UpdateEventsAndAreas(true);
                }
            }

            foreach (var item in notRandomItems.Values)
            {
                RandomizedItems[item] = item;
            }

            foreach (var pair in RandomizedItems)
            {
                DebugString += $"{pair.Key.Name} gives {pair.Value.Name}\n";
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

        private void UpdateEventsAndAreas(bool unlock)
        {
            var changed = false;

            foreach (var ev in events.Values.Where(e => e.Unlocked != unlock))
            {
                var isUnlocked = !ev.RequirementGroups.Any() || ev.RequirementGroups.Any(rg => !rg.Items.Except(RandomizedItems.Values).Any() && rg.Events.All(e => e.Unlocked) && rg.Areas.All(a => a.Unlocked) && events.Values.Where(e => e.Unlocked).Sum(e => e.AP) >= rg.AP);
                if (ev.Unlocked != isUnlocked)
                {
                    ev.Unlocked = isUnlocked;
                    changed = true;
                }
                if (ev.Unlocked)
                {
                    ev.ImportantGroups = [];
                    foreach (var group in ev.RequirementGroups)
                    {
                        if (!group.Items.Except(RandomizedItems.Values).Any() && group.Areas.All(a => a.Unlocked) && group.Events.All(e => e.Unlocked))
                        {
                            ev.ImportantGroups.Add(group);
                        }
                    }
                }

                if (unlock && isUnlocked) debugQueue += $"EVENT {ev.Name} Unlocked\n";
                else if (!unlock && !isUnlocked) debugQueue += $"EVENT {ev.Name} Relocked\n";
            }

            foreach (var area in areas.Values.Where(a => a.Unlocked != unlock))
            {
                var isUnlocked = !area.RequirementGroups.Any() || area.RequirementGroups.Any(rg => !rg.Items.Except(RandomizedItems.Values).Any() && rg.Events.All(e => e.Unlocked) && rg.Areas.All(a => a.Unlocked));
                if (area.Unlocked != isUnlocked)
                {
                    area.Unlocked = isUnlocked;
                    changed = true;
                }
                if (area.Unlocked)
                {
                    area.ImportantGroups = [];
                    foreach (var group in area.RequirementGroups)
                    {
                        if (!group.Items.Except(RandomizedItems.Values).Any() && group.Areas.All(a => a.Unlocked) && group.Events.All(e => e.Unlocked))
                        {
                            area.ImportantGroups.Add(group);
                        }
                    }
                }

                if (unlock && isUnlocked) debugQueue += $"AREA {area.Name} Unlocked\n";
                else if (!unlock && !isUnlocked) debugQueue += $"AREA {area.Name} Relocked\n";
            }

            if (changed) UpdateEventsAndAreas(unlock);
        }
    }
}