using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EmberscarredWhelp
    {
        internal static OfflinePoolFilter Pool => OfflinePoolFilter.Discover(minCost:5,maxCost:5);
        [OfflinePool("FIR_927",115656)]
        internal static PoolRequirement[] Pools() => new[]{new PoolRequirement("five-cost",Pool,new[]{"SHAMAN"})};
        [StandardCard("FIR_927",115656)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new DecisionBattlecryTask("client/FIR_927/v1",
                new DiscoverThenContinueTask(Pool,recipeDbfId:115656),
                new CustomTask((g,c,s,t,stack)=>{
                    var chosen=stack.Playables.SingleOrDefault();
                    if(chosen!=null && Generic.RemoveFromZone(c,chosen)) Generic.AddHandPhase(c,chosen);
                    TemporaryRules.NextTurnMana(c);
                })) };
        }
    }
}
