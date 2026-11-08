using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TimeLostGlaive
    {
        [StandardCard("TIME_444",120993)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { DeathrattleTask=new CustomTask((g,c,s,t,stack)=> {
                var picked=TimeTravelPastPools.Pick(g,c,s,"TIME_444","demon");
                if(picked!=null) new EffectContext(g,c.Id,s.Id,stack:stack).AddToHand(picked.Id);
            }) };
        }
    }
}
