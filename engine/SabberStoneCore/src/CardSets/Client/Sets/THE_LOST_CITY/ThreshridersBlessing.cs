using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ThreshridersBlessing
    {
        internal const int DbfId = 119587;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = new AddEnchantmentTask("TLC_477e", EntityType.TARGET) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power {
                Enchant = new Enchant(Effects.AttackHealth_N(4)),
                DeathrattleTask = EffectContext.Task(ctx => {
                    var chosen = ctx.PickRandomCard(TheLostCityPools.FourCostMinion(), recipeDbfId: DbfId);
                    if (chosen != null) ctx.RunLegacy(new SummonTask(chosen.Id, SummonSide.RIGHT));
                    return TaskState.COMPLETE;
                })
            };
        }
    }
}
