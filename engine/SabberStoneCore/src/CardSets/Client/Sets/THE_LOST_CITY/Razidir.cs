using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Razidir
    {
        internal const int DbfId = 118210;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int repeats = KindredRules.BeginMatchedClause((IPlayable)s);
                var owner = repeats > 0 ? c.Opponent : c;
                for (int i = 0; i < (repeats > 0 ? repeats : 1) && !owner.HandZone.IsEmpty; i++)
                {
                    g.OnRandomHappened(true);
                    Generic.DiscardBlock(owner, owner.HandZone[g.Random.Next(owner.HandZone.Count)]);
                }
            }) };
        }
    }
}
