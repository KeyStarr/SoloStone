using SabberStoneCore.Conditions;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StormTheGates
    {
        [StandardCard("TLC_EVENT_400", 120648)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            QuestDefinition.Register(new QuestDefinition(120648, 3, QuestRewardKind.CardToHand, "ICC_828t"));
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION) {
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(played => {
                    var races = played.Game.HistoryFacts.Query.Plays().LastOrDefault(f => f.EntityId == played.Id)?.Races ?? played.Card.Races;
                    return races.Contains(Race.BEAST) || races.Contains(Race.UNDEAD) || races.Contains(Race.ALL);
                }),
                SingleTask = new CustomTask((g,c,s,t,stack) => QuestRules.Advance((Spell)s))
            } };
        }
    }
}
namespace SabberStoneCore.CardSets.Client
{
    internal static class StormTheGatesPools
    {
        internal static OfflinePoolFilter Beasts(PoolScope scope) => new OfflinePoolFilter(scope,
            type: CardType.MINION, maxCost: 5, race: Race.BEAST, anyOf: new[] {
                new OfflinePoolFilter(scope, PoolClassMode.Exact, CardClass.HUNTER),
                new OfflinePoolFilter(scope, PoolClassMode.Exact, CardClass.NEUTRAL) });
        [OfflinePool("TLC_EVENT_400", 120648)]
        internal static PoolRequirement[] Declare() => new[] {
            new PoolRequirement("event-zombeast-standard", Beasts(PoolScope.Standard), new[] { "NEUTRAL" }),
            new PoolRequirement("event-zombeast-wild", Beasts(PoolScope.Wild), new[] { "NEUTRAL" })
        };
    }
}
