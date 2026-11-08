using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BloodthistleIllusionist
    {
        [StandardCard("EDR_780", 115174)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => NativeTailRules.CopyIllusion((SabberStoneCore.Model.Entities.IPlayable)s)) };
        }
        [StandardCard("EDR_780e1", 117301)]
        internal static void Ambiguous(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}
