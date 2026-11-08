using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MiracleGrowth
    {
        [StandardCard("TSC_656", 73471)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                new DrawTask(3).Process(g,c,s,t,stack);
                if (c.BoardZone.IsFull) return;
                var plant = (Minion)Entity.FromCard(c, Cards.FromId("TSC_656t"), new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                Effects.SetAttack(c.HandZone.Count).ApplyTo(plant);
                Effects.SetMaxHealth(c.HandZone.Count).ApplyTo(plant);
                Generic.SummonBlock(g, plant, -1, s);
            }) };
        }
        [StandardCard("TSC_656t", 78670)]
        internal static void Plant(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}
