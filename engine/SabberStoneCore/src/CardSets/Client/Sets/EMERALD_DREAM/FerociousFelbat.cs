using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FerociousFelbat
    {
        internal const int DbfId = 114498;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                if (c.BoardZone.IsFull) return;
                var pool = c.GraveyardZone.OfType<Minion>()
                    .Where(m => m.ToBeDestroyed && m.Card.Deathrattle && m.Card.Cost >= 5 && m.Card.Id != s.Card.Id).ToArray();
                if (pool.Length == 0) return;
                var selected = pool.Length == 1 ? pool[0] : pool[g.Random.Next(pool.Length)];
                if (pool.Length > 1) g.OnRandomHappened(true);
                int position = EffectContext.DeathrattlePosition(c, s);
                var raised = new TaskStack { Number = position + 1 };
                new SummonCopyTask(EntityType.TARGET, addToStack: true, side: SummonSide.NUMBER)
                    .Process(g, c, s, selected, raised);
                if (raised.Playables?.FirstOrDefault() is IPlayable revived)
                    new SummonCopyTask(EntityType.TARGET, side: SummonSide.NUMBER)
                        .Process(g, c, s, revived, new TaskStack { Number = position + 2 });
            }) };
        }
    }
}
