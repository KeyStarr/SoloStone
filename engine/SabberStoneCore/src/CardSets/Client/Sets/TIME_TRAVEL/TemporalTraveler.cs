using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Tasks;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TemporalTraveler
    {
        internal const int DbfId = 120527;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int position = EffectContext.DeathrattlePosition(c, s);
                int shadow = ctx.Summon("TIME_434t", position);
                if (shadow == 0) return;
                var targets = new TaskStack { Playables = c.Opponent.BoardZone
                    .Where(m => !m.IsDead && !m.Untouchable).Cast<IPlayable>().ToArray() };
                if (new RandomTask(1, EntityType.STACK).Process(g, c, s, null, targets) == TaskState.STOP) return;
                new AttackTask(EntityType.SOURCE, EntityType.STACK).Process(g, c, g.IdEntityDic[shadow], null, targets);
            }) };
        }
    }
}
