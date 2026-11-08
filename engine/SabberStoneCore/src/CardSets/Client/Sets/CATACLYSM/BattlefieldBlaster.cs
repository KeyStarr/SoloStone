using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.Conditions;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BattlefieldBlaster
    {
        internal const int DbfId = 123124;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CATA_209/v1",
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Hand, cardType: CardType.SPELL)),
                EffectContext.Task(ctx => ctx.LastChosenEntityId == 0 ? TaskState.COMPLETE
                    : ctx.GiveBuff(ctx.LastChosenEntityId, "CATA_209e"))) };
        }
        internal static void ConfigureBuff(Card card) => ArchmageKalec.ConfigureBuff(card);
    }
}
