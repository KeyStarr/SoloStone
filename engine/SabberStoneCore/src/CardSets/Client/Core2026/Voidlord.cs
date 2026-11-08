using System;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Core2026
{
    internal static class Voidlord
    {
        internal const int DbfId = 126893;
        internal static void Configure(Card card)
        {
            // Text is rules-equivalent, but legacy LOOT_368 appends its Demons at the far right.
            // Retail fills the dying Voidlord's slot rightward.
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int position = EffectContext.DeathrattlePosition(c, s);
                for (int i = 0; i < 3; i++)
                    if (ctx.Summon("CS2_065", Math.Min(position, c.BoardZone.SlotCount)) != 0) position++;
            }) };
        }
    }
}
