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
    internal static class VanessatheRingleader
    {
        internal const int DbfId = 124150;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_PLAY_CARD) {
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var chosen = new EffectContext(g, c.Id, s.Id, stack: stack)
                        .PickRandomCard(EscapeFromVioletHoldPools.BattlecryMinion(), recipeDbfId: DbfId);
                    if (chosen == null) return;
                    var generated = Entity.FromCard(c, chosen);
                    generated[GameTag.DISPLAYED_CREATOR] = s.Id;
                    generated.Cost -= 2;
                    Generic.AddHandPhase(c, generated);
                })
            } };
        }
    }
}
