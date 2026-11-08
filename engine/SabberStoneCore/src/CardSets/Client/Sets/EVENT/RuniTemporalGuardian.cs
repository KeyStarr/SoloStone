using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RuniTemporalGuardian
    {
        [StandardCard("TIME_EVENT_998", 121676)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => HandTimelineRules.SendMinions((IPlayable)s)) };
        }
        [StandardCard("TIME_EVENT_998e1", 123414)]
        internal static void Schedule(Card card)
        { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power(); }
        [StandardCard("TIME_EVENT_998e2", 123415)]
        internal static void Buff(Card card)
        { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(5)) }; }
    }
}
