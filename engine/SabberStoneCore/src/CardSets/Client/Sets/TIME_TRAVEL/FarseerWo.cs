using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FarseerWo
    {
        [StandardCard("TIME_013",119686)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Trigger=new Trigger(TriggerType.AFTER_CAST) {
                TriggerSource=TriggerSource.FRIENDLY,
                SingleTask=new CustomTask((g,c,s,t,stack)=> {
                    var offers=TimeTravelPastPools.Offers(g,c,s,"TIME_013","nature");
                    if(offers.Length==0) offers=TimeTravelPastPools.Offers(g,c,s,"TIME_013","fallback",CardClass.SHAMAN);
                    TimeTravelPastPools.Open(g,c,s,offers);
                })
            } };
        }
    }
}
