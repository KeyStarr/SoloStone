using SabberStoneCore.Actions;
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
    internal static class Vulcanos
    {
        internal const int DbfId = 123665;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.Declare(card, "CATA_488t", "CATA_488t2");
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new DamageTask(3, EntityType.ALLMINIONS_NOSOURCE)
            } };
        }
        internal static void ConfigurePlume(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TAKE_DAMAGE) {
                TriggerSource = TriggerSource.SELF,
                Condition = new SelfCondition(p => p.Game.CurrentEventData?.EventNumber > 0),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var chosen = new EffectContext(g, c.Id, s.Id, stack: stack)
                        .PickRandomCard(CataclysmPools.FireSpell(), recipeDbfId: DbfId);
                    if (chosen == null) return;
                    var generated = (Playable)Entity.FromCard(c, chosen);
                    generated[GameTag.DISPLAYED_CREATOR] = s.Id;
                    CostRules.AddModifier(generated, EffectOperator.SUB, 3);
                    Generic.AddHandPhase(c, generated);
                })
            } };
        }
    }
}
