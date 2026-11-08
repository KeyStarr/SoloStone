using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MalevolentMutant
    {
        internal const int DbfId = 123604;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CATA_697/v1",
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Hand, cardType: CardType.SPELL, spellSchool: 7)),
                EffectContext.Task(ctx => ctx.LastChosenEntityId == 0 ? TaskState.COMPLETE :
                    ctx.RunLegacy(new CopyTask(EntityType.TARGET, Zone.HAND), ctx.LastChosenEntityId))) };
        }
    }
}
