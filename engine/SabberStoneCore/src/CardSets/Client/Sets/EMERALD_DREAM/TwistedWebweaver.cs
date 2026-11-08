using System.Linq;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TwistedWebweaver
    {
        internal const int DbfId = 115612;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            // Rule: same exact card ID, prior friendly hand-play; trigger before Battlecry, excluding this occurrence and self.
            card.Power = new Power { Trigger = new Trigger(TriggerType.PLAY_MINION) {
                TriggerSource = TriggerSource.MINIONS,
                Condition = new SelfCondition(p => p.Game.HistoryFacts.Query.Plays()
                    .Count(f => f.ControllerId == p.Controller.Id && f.DbfId == p.Card.AssetId && f.CardType == CardType.MINION) >= 2),
                SingleTask = new DrawTask(1)
            } };
        }
    }
}
