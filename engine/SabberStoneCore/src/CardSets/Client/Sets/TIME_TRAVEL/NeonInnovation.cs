using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NeonInnovation
    {
        [StandardCard("TIME_016",119964)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new DecisionSpellTask("client/TIME_016/v1",
                new DiscoverThenContinueTask((g,c,s,stack)=>TimeTravelPastPools.Offers(g,c,s,"TIME_016","mech"), DiscoverRealRules.PrepareDiscover),
                new CustomTask((g,c,s,t,stack)=> {
                    foreach(var chosen in stack.Playables) {
                        Generic.AddEnchantmentBlock(g,Cards.FromId("TIME_016e"),(IPlayable)s,chosen);
                        Generic.RemoveFromZone(c,chosen); Generic.AddHandPhase(c,chosen);
                    }
                })) };
        }
        [StandardCard("TIME_016e",123406)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Enchant=new Enchant(Effects.Attack_N(5),Effects.Health_N(5)) };
        }
    }
}
