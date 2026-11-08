using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SpawnOfCthun
    {
        internal const int DbfId = 132347;
        internal static bool Ready(IPlayable source) => source.Controller.DeckCards.Any(c => c.Id == "BE_013");
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(Ready);
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/BE_014/v1",
                new ConditionTask(EntityType.SOURCE,new SelfCondition(Ready)),
                new FlagTask(false,EffectContext.Task(_ => TaskState.STOP)),
                CthunRules.ExternalAbility("client/BE_014/ability")) };
        }
    }
}
