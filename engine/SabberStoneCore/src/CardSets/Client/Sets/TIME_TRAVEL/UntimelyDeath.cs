using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class UntimelyDeath
    {
        [StandardCard("TIME_620", 121210)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.DEATH) {
                TriggerSource = TriggerSource.MINIONS,
                Condition = new SelfCondition(minion => Qualifies(minion)),
                RemoveAfterTriggered = true,
                SingleTask = new CustomTask((g,c,s,t,stack) => {
                    s[LifecycleRules.DeathContinuationTarget] = t.Id;
                })
            } };
        }
        private static bool Qualifies(IPlayable minion)
        {
            var g=minion.Game; var owner=minion.Controller;
            if (g.CurrentPlayer == owner) return false;
            var death=g.HistoryFacts.Query.Deaths().LastOrDefault(d => d.EntityId == minion.Id);
            return death != null && death.Turn == g.Turn && death.FromPlayId != 0 && death.PlayedTurn == g.Turn-1
                && g.HistoryFacts.Query.Plays().Any(p => p.PlayId == death.FromPlayId && p.ControllerId == owner.Id);
        }
    }
}
