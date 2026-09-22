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

        Random r;

        private Dictionary<(Item Pickup, Item Reward), HashSet<Item>> hypotheticalUnlockCache = new();

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

        public RandomizerSettings Settings { get; set; }

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

        public byte[] MusicTracks { get; private set; } = [0, 1, 2, 3, 8, 9, 10, 11, 16, 17, 18, 19, 20, 24, 25, 26, 27, 32, 33, 34, 40, 41, 42, 48, 56];

        public int Seed { get; private set; }

        public bool ResetTracker { get; set; }
        public List<byte> ItemTrackerStatus { get; set; } = [];

        public void Randomize() => Randomize((int)DateTime.Now.Ticks);

        public void Randomize(int seed)
        {
            Seed = seed;

            r = new(seed);

            RandomizedItems = [];
            hypotheticalUnlockCache.Clear();
            UpdateItems();

            var stack = new List<PlacementStep>();
            var backtrackCount = 0;
            var furthestProgress = 0;
            var stuckStreak = 0;

            while (RandomItemPool.Count > 0)
            {
                var availableItemsToRandomize = AvailableItemsToRandomize;

                if (availableItemsToRandomize.Count == 0)
                {
                    if (!Backtrack(stack, ref backtrackCount, ref stuckStreak))
                    {
                        throw new InvalidOperationException($"Randomization is unsolvable: exhausted all backtracking options after {backtrackCount} backtracks.");
                    }
                    continue;
                }

                var step = new PlacementStep();
                Item itemToRandomize;
                Item reward;

                if (availableItemsToRandomize.Count <= 2 && RandomItemPool.Count > 1)
                {
                    itemToRandomize = availableItemsToRandomize[0];

                    var potentialUnlocks = RandomItemPool
                        .Select(candidateReward => new
                        {
                            Item = candidateReward,
                            Unlocks = GetHypotheticalUnlocks(itemToRandomize, candidateReward)
                        }).Where(i => i.Unlocks.Count > 0).ToList();

                    if (potentialUnlocks.Count > 0)
                    {
                        var totalWeight = potentialUnlocks.Sum(p => p.Unlocks.Count);

                        var roll = r.Next(totalWeight);
                        var cumulativeWeight = 0;
                        reward = potentialUnlocks[^1].Item;

                        foreach (var candidate in potentialUnlocks)
                        {
                            cumulativeWeight += candidate.Unlocks.Count;

                            if (roll < cumulativeWeight)
                            {
                                reward = candidate.Item;
                                break;
                            }
                        }
                    }
                    else
                    {
                        reward = RandomItemPool[r.Next(RandomItemPool.Count)];
                    }
                }
                else
                {
                    itemToRandomize = availableItemsToRandomize[r.Next(availableItemsToRandomize.Count)];
                    reward = RandomItemPool[r.Next(RandomItemPool.Count)];
                }

                step.Location = itemToRandomize;
                step.ExcludedRewards.Add(reward);
                RandomizedItems[itemToRandomize] = reward;
                stack.Add(step);

                if (stack.Count > furthestProgress)
                {
                    furthestProgress = stack.Count;
                    stuckStreak = 0;
                }

                hypotheticalUnlockCache.Clear();
                UpdateItems();
            }

            foreach (var item in notRandomItems.Values)
            {
                RandomizedItems[item] = item;
            }

            foreach (var pair in RandomizedItems.Where(i => !i.Key.NotRandom))
            {
                DebugString += $"{pair.Key.Name} gives {pair.Value.Name}\n";
            }

            if (Settings.ShuffleMusic)
            {
                ShuffleMusic();
            }

        }

        private class PlacementStep
        {
            public Item Location;
            public HashSet<Item> ExcludedRewards = new();
        }

        private const int StuckThreshold = 15;

        private bool Backtrack(List<PlacementStep> stack, ref int backtrackCount, ref int stuckStreak)
        {
            stuckStreak++;

            var jumpLevel = stuckStreak / StuckThreshold;
            var jump = jumpLevel == 0 ? 0 : 1 << Math.Min(jumpLevel, 30); // avoid overflow on very long stuck streaks
            var extraFramesToDiscard = Math.Min(stack.Count > 0 ? stack.Count - 1 : 0, jump);

            for (var i = 0; i < extraFramesToDiscard; i++)
            {
                backtrackCount++;
                var skippedFrame = stack[^1];
                stack.RemoveAt(stack.Count - 1);
                RandomizedItems.Remove(skippedFrame.Location);
            }

            if (extraFramesToDiscard > 0)
            {
                hypotheticalUnlockCache.Clear();
                UpdateItems(fullRecompute: true);
            }

            while (stack.Count > 0)
            {
                backtrackCount++;

                var frame = stack[^1];
                stack.RemoveAt(stack.Count - 1);

                RandomizedItems.Remove(frame.Location);
                hypotheticalUnlockCache.Clear();
                UpdateItems(fullRecompute: true);

                var candidates = RandomItemPool.Where(i => !frame.ExcludedRewards.Contains(i)).ToList();

                if (candidates.Count > 0)
                {
                    var reward = candidates[r.Next(candidates.Count)];
                    frame.ExcludedRewards.Add(reward);
                    RandomizedItems[frame.Location] = reward;
                    stack.Add(frame);

                    hypotheticalUnlockCache.Clear();
                    UpdateItems();
                    return true;
                }

            }

            return false;
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

        private void UpdateItems(bool fullRecompute = false)
        {
            var unlockedItems = fullRecompute ? new HashSet<Item>() : items.Values.Where(i => i.Unlocked).ToHashSet();
            var unlockedEvents = fullRecompute ? new HashSet<Event>() : events.Values.Where(e => e.Unlocked).ToHashSet();
            var unlockedAreas = fullRecompute ? new HashSet<Area>() : areas.Values.Where(a => a.Unlocked).ToHashSet();

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
            if (hypotheticalUnlockCache.TryGetValue((pickup, reward), out var cachedUnlocks)) return cachedUnlocks;

            var initiallyUnlockedItems = items.Values.Where(i => i.Unlocked).ToHashSet();

            var unlockedItems = new HashSet<Item>(initiallyUnlockedItems);
            var unlockedEvents = events.Values.Where(e => e.Unlocked).ToHashSet();
            var unlockedAreas = areas.Values.Where(a => a.Unlocked).ToHashSet();

            var hypotheticalRandomizedItems =
                new Dictionary<Item, Item>(RandomizedItems)
                {
                    [pickup] = reward
                };

            CalculateUnlockState(unlockedItems, unlockedEvents, unlockedAreas, hypotheticalRandomizedItems);

            var unlocks = unlockedItems.Except(initiallyUnlockedItems).ToHashSet();

            hypotheticalUnlockCache[(pickup, reward)] = unlocks;

            return unlocks;
        }

        private void ShuffleMusic() => r.Shuffle(MusicTracks);
    }
}