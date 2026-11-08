using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DiabolusRex
    {
        internal const int DbfId = 118507;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int repeats = KindredRules.BeginMatchedClause((IPlayable)s);
                for (int repeat = 0; repeat < repeats; repeat++)
                {
                    var enemies = c.Opponent.BoardZone.Where(m => !m.Untouchable && (repeat == 0 || !m.IsDead)).ToArray();
                    if (enemies.Length == 0) return;
                    var targets = new[] { enemies.First(), enemies.Last() }.Distinct().ToArray();
                    foreach (var minion in targets)
                    {
                        if (g.History) g.PowerHistory.SelectResolvedTarget(minion);
                        Generic.DamageCharFunc((IPlayable)s, minion, 6, false);
                    }
                }
            }) };
        }
    }
}
