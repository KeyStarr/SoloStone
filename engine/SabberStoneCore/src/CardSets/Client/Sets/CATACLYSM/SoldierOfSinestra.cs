using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SoldierOfSinestra
    {
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_SUMMON) {
                TriggerSource = TriggerSource.SELF,
                SingleTask = new CustomTask((g, c, source, target, stack) => {
                    var ctx = new EffectContext(g, c.Id, source.Id);
                    var definition = ctx.PickRandomCard(CataclysmPools.HeraldOtherClassSpell(), recipeDbfId: 127003);
                    if (definition == null) return;
                    var generated = (Playable)Entity.FromCard(c, definition);
                    generated[GameTag.DISPLAYED_CREATOR] = source.Id;
                    CostRules.AddModifier(generated, EffectOperator.ADD, -HeraldRules.Magnitude((IPlayable)source));
                    Generic.AddHandPhase(c, generated);
                })
            } };
        }
    }
}
