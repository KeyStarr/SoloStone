using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BygoneDoomspeaker
    {
        internal const int DbfId = 119545;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var owner in new[] { c, c.Opponent })
                {
                    if (owner.HandZone.IsEmpty) continue;
                    var discarded = owner.HandZone[g.Random.Next(owner.HandZone.Count)];
                    Generic.DiscardBlock(owner, discarded);
                }
            }) };
        }
    }
}
