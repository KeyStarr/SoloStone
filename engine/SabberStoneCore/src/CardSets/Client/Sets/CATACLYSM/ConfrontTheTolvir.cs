using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ConfrontTheTolvir
    {
        internal const int DbfId = 122939;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                // PlayCard records printed SourceCard before the effect; preserve duplicates and order.
                var minions = c.PlayHistory.Where(p => p.SourceCard.Type == CardType.MINION && p.SourceCard.Cost == 1)
                    .Select(p => p.SourceCard.Id).ToArray();
                foreach (var id in minions) {
                    if (c.BoardZone.IsFull) break;
                    ctx.Summon(id);
                }
            }) };
        }
    }
}
