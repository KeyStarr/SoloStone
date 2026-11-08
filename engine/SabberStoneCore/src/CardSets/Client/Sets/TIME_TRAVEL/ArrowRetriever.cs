using System;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ArrowRetriever
    {
        internal const int DbfId = 120788;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int count = Math.Max(0, 3 - c.HandZone.Count);
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                // Every missing card is a draw attempt, including fatigue on an empty deck.
                for (int i = 0; i < count; i++) ctx.Draw();
            }) };
        }
    }
}
