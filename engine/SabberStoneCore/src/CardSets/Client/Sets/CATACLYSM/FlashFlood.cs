using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FlashFlood
    {
        internal const int DbfId = 122995;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id);
                int iterations = ctx.PlayedFromEdge ? 2 : 1;
                for (int i = 0; i < iterations; i++)
                {
                    var minions = c.Opponent.BoardZone.Where(m => !m.Untouchable && !m.IsDead).ToArray();
                    if (minions.Length == 0) break;
                    // Unpinned: keep both edge entries, including a lone minion; reselect after first-wave deaths.
                    foreach (var minion in new[] { minions[0], minions[minions.Length - 1] })
                        if (minion.Zone == c.Opponent.BoardZone)
                            ctx.Damage(minion.Id, 5, true);
                    if (i + 1 < iterations) g.DeathProcessingAndAuraUpdate();
                }
            }) };
        }
    }
}
