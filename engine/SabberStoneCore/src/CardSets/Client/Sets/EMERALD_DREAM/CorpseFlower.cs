using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CorpseFlower
    {
        internal const int DbfId = 114306;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_SUMMON) {
                TriggerSource = TriggerSource.OP_MINIONS,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (t is Minion minion && minion.Zone == minion.Controller.BoardZone && !minion.IsDead
                        && CorpseRules.TrySpend(c, 2))
                        new EffectContext(g, c.Id, s.Id, t.Id, stack).Damage(t.Id, 3);
                })
            } };
        }
    }
}
