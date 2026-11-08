using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Asphyxiate
    {
        internal const int DbfId = 111317;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var candidates = c.Opponent.BoardZone.Where(m => !m.Untouchable && !m.IsDead).ToArray();
                if (candidates.Length == 0) return;
                int attack = candidates.Max(m => m.AttackDamage);
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                ctx.Destroy(ctx.SampleDistinct(candidates.Where(m => m.AttackDamage == attack).ToArray(), 1)[0].Id);
            }) };
        }
    }
}
