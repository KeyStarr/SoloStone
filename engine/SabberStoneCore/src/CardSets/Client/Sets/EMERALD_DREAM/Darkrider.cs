using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Darkrider
    {
        internal const int DbfId = 114218;
        internal static readonly OfflinePoolFilter Filter = OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.DRAGON);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(EmeraldDreamConditions.HoldingDragon);
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/EDR_456/v1",
                new HoldingDragonTask(),
                new DiscoverWithDarkGiftThenContinueTask(Filter, recipeDbfId: DbfId), new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null) return;
                    Generic.RemoveFromZone(c, chosen);
                    Generic.AddHandPhase(c, chosen);
                    DarkGifts.ResolveSweetDreams(chosen);
                })) };
        }

        private sealed class HoldingDragonTask : SimpleTask
        {
            public override TaskState Process(in Game game, in Controller controller, in IEntity source,
                in IPlayable target, in TaskStack stack = null) =>
                EmeraldDreamConditions.HoldingDragon((IPlayable)source) ? TaskState.COMPLETE : TaskState.STOP;
        }
    }
}
