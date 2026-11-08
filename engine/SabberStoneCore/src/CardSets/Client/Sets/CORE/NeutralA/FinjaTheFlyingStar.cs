using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FinjaTheFlyingStar
    {
        internal const int DbfId = 120428;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Tags[(GameTag)858] = 40803;
            card.Tags[(GameTag)1413] = 40803;
            card.Power = new Power
            {
                Trigger = new Trigger(TriggerType.AFTER_ATTACK)
                {
                    TriggerSource = TriggerSource.SELF,
                    Condition = new SelfCondition(source => source.Game.CurrentEventData?.EventTarget is Minion defender && defender.ToBeDestroyed),
                    // A shared StateTaskList mutates its State on the first cloned trigger.
                    SingleTask = new CustomTask((g, c, s, t, stack) =>
                    {
                        var candidates = c.DeckZone.OfType<Minion>().Where(m => m.IsRace(Race.MURLOC)).ToArray();
                        // Order is random even with exactly two candidates: only the first fits on a six-slot board.
                        var selected = candidates.Length > 1 ? candidates.ChooseNElements(2, g.Random) : candidates;
                        if (candidates.Length > 1) g.OnRandomHappened(true);
                        int position = EffectContext.DeathrattlePosition(c, s);
                        foreach (var minion in selected)
                        {
                            if (c.BoardZone.IsFull) break;
                            minion.Zone.Remove(minion);
                            Generic.SummonBlock(g, minion, Math.Min(position++, c.BoardZone.SlotCount), s);
                        }
                    })
                }
            };
        }
    }
}
