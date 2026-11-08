using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class VerdantDreamsaber
    {
        internal const int DbfId = 113371;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CostsThreeOrLess);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var source = (Minion)s;
                if (!CostsThreeOrLess(source)) return;
                var pool = c.Opponent.BoardZone.GetAll();
                if (pool.Length == 0) return;
                var targets = new EffectContext(g, c.Id, s.Id, stack: stack).SampleDistinct(pool, 2);
                g.OnRandomHappened(true);
                foreach (var enemy in targets) {
                    if (source.Zone != c.BoardZone || source.ToBeDestroyed) break;
                    if (enemy.Zone != c.Opponent.BoardZone || enemy.ToBeDestroyed) continue;
                    new AttackTask(EntityType.SOURCE, EntityType.TARGET).Process(g, c, s, enemy, stack);
                }
            }) };
        }

        // HandZone.Remove resets Cost; payment preserves the cost used by this play.
        private static bool CostsThreeOrLess(IPlayable source) =>
            (source.Zone == source.Controller.HandZone ? source.Cost : source[GameTag.TAG_LAST_KNOWN_COST_IN_HAND]) <= 3;
    }
}
