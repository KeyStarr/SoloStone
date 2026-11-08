using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HeartrootStones
    {
        internal const int DbfId = 123481;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.PoweredUpWhen(NoMinionPlayedPreviousTurn);
            // "Last turn" = the controller's previous own turn (as MEND_041); with no previous own turn no minion was played.
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                bool again = NoMinionPlayedPreviousTurn(ctx.History, ctx.ControllerId);
                for (int i = 0; i < (again ? 2 : 1); i++)
                {
                    ctx.Draw();
                    ctx.RunLegacy(new ArmorTask(3));
                }
                return TaskState.COMPLETE;
            }) };
        }

        private static bool NoMinionPlayedPreviousTurn(IPlayable source) =>
            NoMinionPlayedPreviousTurn(source.Game.HistoryFacts.Query, source.Controller.Id);
        private static bool NoMinionPlayedPreviousTurn(HistoryQuery history, int controllerId) =>
            !history.Plays(history.PreviousTurnOf(controllerId))
                .Any(f => f.ControllerId == controllerId && f.CardType == CardType.MINION);
    }
}
