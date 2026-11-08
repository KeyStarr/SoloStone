using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WingsOfEternity
    {
        [StandardCard("END_027",121190)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>
                new EffectContext(g,c.Id,s.Id,stack:stack).DiscoverWithDarkGift(TimeTravelPastPools.Offers(g,c,s,"END_027","dragon"))) };
        }
    }
}
