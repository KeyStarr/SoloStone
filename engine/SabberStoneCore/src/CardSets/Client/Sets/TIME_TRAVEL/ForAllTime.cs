using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ForAllTime
    {
        internal const int DbfId = 121191;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Export tag marks Overload as a keyword, while the text specifies its amount.
            card.Overload = 2;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var targets = c.BoardZone.GetAll().Concat(c.Opponent.BoardZone.GetAll())
                    .Where(m => m.AttackDamage <= 4).ToArray();
                foreach (var minion in targets) ctx.Destroy(minion.Id);
            }) };
        }
    }
}
