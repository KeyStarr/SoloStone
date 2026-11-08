using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BlessingOfTheBronze
    {
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new System.Collections.Generic.Dictionary<PlayReq, int> { { PlayReq.REQ_HAND_NOT_FULL, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int n = s[GameTag.TAG_SCRIPT_DATA_NUM_1];
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                // Lean_ imbue-23: resolve class from controller, including copied powers.
                var chosen = ctx.PickRandomCard(TimeTravelPools.BronzeMinion(), recipeDbfId: 121243);
                if (chosen == null) return;
                var generated = Entity.FromCard(c, chosen, new EntityData {
                    [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id
                });
                Generic.AddEnchantmentBlock(g, Cards.FromId("END_000e"), (IPlayable)s, generated, n);
                Generic.AddHandPhase(c, generated);
            }) };
        }
    }
}
