using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RatBurglar
    {
        internal const int DbfId = 131316;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            // Rule: latest successful entry this global turn qualifies; steal left-to-right, burning overflow via normal hand admission (history-facts R8).
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var cards = c.Opponent.HandZone.Where(p => {
                        var entry = g.HistoryFacts.Query.LatestHandEntry(p.Id);
                        return entry != null && entry.ControllerId == c.Opponent.Id && entry.Turn == g.Turn;
                    }).ToArray();
                    foreach (var stolen in cards)
                    {
                        using var historyTransfer=g.Facts.CardTransfer(stolen,c.Opponent.Id,Zone.HAND,s);
                        using var temporaryTransfer = TemporaryRules.PreserveHandTransfer(stolen);
                        if (stolen.OngoingEffect is AdaptiveCostEffect costEffect) costEffect.Remove();
                        // Hand-to-hand theft preserves permanent costs; normal Remove resets them.
                        c.Opponent.HandZone.RemoveForTrade(stolen);
                        stolen.Controller = c;
                        stolen[GameTag.CONTROLLER] = c.PlayerId;
                        Generic.AddHandPhase(c, stolen);
                    }
                })
            } };
        }
    }
}
