using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ShrineOfTwilight
    {
        [StandardCard("CATA_492", 122712)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation { RequiresFreeSlot=true, Task=new CustomTask((g,c,s,t,stack) => {
                HeraldRules.Resolve(c,s); Generic.Draw(c);
            }) } };
        }
    }
}
