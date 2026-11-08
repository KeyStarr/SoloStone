using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AlterTime
    {
        [StandardCard("TIME_857",119633)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            var discover=new DiscoverThenContinueTask((g,c,s,stack)=>TimeTravelPastPools.Offers(g,c,s,"TIME_857","arcane"), DiscoverRealRules.PrepareDiscover);
            var collect=new CustomTask((g,c,s,t,stack)=> {
                foreach(var chosen in stack.Playables) {
                    CostRules.AddModifier((Playable)chosen,EffectOperator.SUB,2);
                    Generic.RemoveFromZone(c,chosen); Generic.AddHandPhase(c,chosen);
                }
                stack.Playables.Clear();
            });
            card.Power=new Power { PowerTask=new DecisionSpellTask("client/TIME_857/v1",discover,collect,discover,collect) };
        }
    }
}
