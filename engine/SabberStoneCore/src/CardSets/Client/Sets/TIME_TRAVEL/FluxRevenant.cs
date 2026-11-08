using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FluxRevenant
    {
        internal const int DbfId = 120175;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.PREDAMAGE) {
                TriggerSource = TriggerSource.SELF,
                FastExecution = true,
                Condition = new SelfCondition(me => {
                    var data = me.Game.CurrentEventData;
                    return me is Character character && !character.IsImmune
                        && data != null && data.EventNumber > 0 && data.EventSource.Controller == me.Controller
                        && PrimordialOverseer.IsNatureSpell(data.EventSource.Card);
                }),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    // Character.TakeDamage reads EventNumber again after PREDAMAGE tasks.
                    g.CurrentEventData.EventNumber = 0;
                    new EffectContext(g, c.Id, s.Id, stack: stack).GiveBuff(s.Id, "TIME_214e");
                })
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2), Effects.Health_N(1)) };
        }
    }
}
