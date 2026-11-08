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
    internal static class SpearheartSentry
    {
        internal const int DbfId = 123112;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var chosen = new EffectContext(g, c.Id, s.Id, stack: stack)
                        .PickRandomCard(CataclysmPools.HolySpell(), recipeDbfId: DbfId);
                    if (chosen == null) return;
                    var generated = Entity.FromCard(c, chosen);
                    generated[GameTag.DISPLAYED_CREATOR] = s.Id;
                    generated.Cost -= 3;
                    Generic.AddHandPhase(c, generated);
                })
            } };
        }
    }
}
