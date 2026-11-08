using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Flashback
    {
        [StandardCard("TIME_711",120793)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=> {
                var ctx=new EffectContext(g,c.Id,s.Id,stack:stack);
                for(int i=0;i<2;i++) {
                    var picked=TimeTravelPastPools.Pick(g,c,s,"TIME_711","one"); if(picked==null) continue;
                    int summoned=ctx.Summon(picked.Id);
                    if(summoned!=0 && c.IsComboActive) ctx.GiveBuff(summoned,"TIME_711e");
                }
            }) };
            card.Power.ComboTask=card.Power.PowerTask;
        }
        [StandardCard("TIME_711e",122828)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Enchant=new Enchant(Effects.Attack_N(1)) };
        }
    }
}
