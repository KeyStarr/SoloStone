using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Wormhole
    {
        internal const int DbfId = 120501;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (c.BoardZone.IsFull) return;
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var chosen = ctx.PickRandomCard(TimeTravelPools.ThreeCostBeast(), recipeDbfId: DbfId);
                if (chosen == null) return;
                int summoned = ctx.Summon(chosen.Id);
                if (summoned == 0) return;
                var targets = new TaskStack();
                if (new RandomTask(1, EntityType.ENEMIES).Process(g, c, s, null, targets) == TaskState.STOP) return;
                new AttackTask(EntityType.SOURCE, EntityType.STACK).Process(g, c, g.IdEntityDic[summoned], null, targets);
            }) };
        }
    }
}
