using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Chronoclaws
    {
        internal const int DbfId = 121158;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.HERO,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (c.HandZone.IsEmpty) return;
                    int cost = c.HandZone.Max(p => p.Cost);
                    var tied = c.HandZone.Where(p => p.Cost == cost).ToArray();
                    Generic.DiscardBlock(c, tied[g.Random.Next(tied.Length)]);
                })
            } };
        }
    }
}
