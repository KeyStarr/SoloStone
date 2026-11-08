using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RavenousFelhunter
    {
        internal const int DbfId = 114497;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                if (c.BoardZone.IsFull) return;
                // Game.GraveYard retains destroyed entities; discard and burn are not deaths.
                var pool = c.GraveyardZone.OfType<Minion>()
                    .Where(m => m.ToBeDestroyed && m.Card.Deathrattle && m.Card.Cost <= 4).ToArray();
                if (pool.Length == 0) return;
                var selected = pool.Length == 1 ? pool[0] : pool[g.Random.Next(pool.Length)];
                if (pool.Length > 1) g.OnRandomHappened(true);
                int position = EffectContext.DeathrattlePosition(c, s);
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int raised = ctx.Summon(selected.Card.Id, position);
                if (raised != 0)
                    new SummonCopyTask(EntityType.TARGET, side: SummonSide.NUMBER)
                        .Process(g, c, s, g.IdEntityDic[raised], new TaskStack { Number = position + 2 });
            }) };
        }
    }
}
