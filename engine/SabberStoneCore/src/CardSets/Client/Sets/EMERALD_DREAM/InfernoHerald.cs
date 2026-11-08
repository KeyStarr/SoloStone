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
    internal static class InfernoHerald
    {
        internal const int DbfId = 115637;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_CAST) {
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(ScorchingWinds.IsFireSpell),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var chosen = new EffectContext(g, c.Id, s.Id, stack: stack)
                        .PickRandomCard(EmeraldDreamPools.Elemental(), recipeDbfId: DbfId);
                    if (chosen == null) return;
                    var generated = Entity.FromCard(c, chosen);
                    generated[GameTag.DISPLAYED_CREATOR] = s.Id;
                    Generic.AddHandPhase(c, generated);
                    if (generated.Zone == c.HandZone)
                        new ApplyEffectTask(EntityType.TARGET, Effects.ReduceCost(3)).Process(g, c, s, generated, stack);
                })
            } };
        }
    }
}
