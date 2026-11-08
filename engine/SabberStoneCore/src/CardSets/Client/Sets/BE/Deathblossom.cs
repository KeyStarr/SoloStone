using System;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Deathblossom
    {
        internal const int DbfId = 131180;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddCardTo("BE_802t", EntityType.HAND, 2) };
        }
        internal static void ConfigurePlant(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                if (c.BoardZone.IsFull) return;
                var plant = (Minion)s;
                int position = plant.Zone == c.BoardZone ? plant.ZonePosition + 1 : plant.LastBoardPosition;
                new EffectContext(g, c.Id, s.Id, stack: stack).Summon("BE_002", Math.Min(position, c.BoardZone.SlotCount));
            }) };
        }
    }
}
