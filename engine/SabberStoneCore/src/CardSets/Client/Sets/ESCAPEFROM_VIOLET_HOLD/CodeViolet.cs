using System;
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
    internal static class CodeViolet
    {
        internal const int DbfId = 126841;
        [StandardCard("JAIL_735", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.PoweredUpWhen(Ready);
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Progress);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int count = OtherCasts((IPlayable)s) >= 3 ? 2 : 1;
                var context = new EffectContext(g, c.Id, s.Id);
                for (int i = 0; i < count; i++)
                {
                    var chosen = context.PickRandomCard(B14K3VioletHoldPools.Minion(), recipeDbfId: DbfId);
                    if (chosen != null) context.Summon(chosen.Id);
                }
            }) };
        }
        private static bool Ready(IPlayable source) => OtherCasts(source) >= 3;
        // SDN1 is progress (ENTITY_TAG_THRESHOLD 2459=2, 2460=3 -> client prints 3-SDN1 left, Ready at >= 3), not remaining.
        private static int Progress(IPlayable source) => Math.Min(3, OtherCasts(source));
        private static int OtherCasts(IPlayable source)
        {
            var history = source.Game.HistoryFacts.Query;
            return history.Casts(history.ThisTurn).Count(f => f.ControllerId == source.Controller.Id
                && f.EntityId != source.Id && f.CasterKind == HistoryCastKind.Player && f.Outcome == HistoryCastOutcome.Completed);
        }
    }
}
