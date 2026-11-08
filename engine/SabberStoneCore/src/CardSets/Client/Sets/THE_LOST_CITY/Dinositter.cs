using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Dinositter
    {
        internal const int DbfId = 117574;
        // Rule: owner's end turn selects a current hand Beast; ordinary permanent cost change copies in hand and resets on bounce.
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var beasts = c.HandZone.Where(p => p.Card.IsRace(Race.BEAST)).ToArray();
                    if (beasts.Length == 0) return;
                    g.OnRandomHappened(true);
                    new ApplyEffectTask(EntityType.TARGET, Effects.ReduceCost(1)).Process(g, c, s, beasts[g.Random.Next(beasts.Length)], stack);
                })
            } };
        }
    }
}
