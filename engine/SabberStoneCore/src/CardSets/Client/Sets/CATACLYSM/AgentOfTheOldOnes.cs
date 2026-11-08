using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AgentOfTheOldOnes
    {
        internal const int DbfId = 123060;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CATA_200/v1",
                new SelectEntityThenContinueTask(),
                EffectContext.Task(ctx => ctx.LastChosenEntityId == 0 ? TaskState.COMPLETE :
                    ctx.Transform(ctx.LastChosenEntityId, "GAME_005"))) };
        }
    }
}
