using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class YseraEmeraldAspect
    {
        [StandardCard("EDR_000", 113321)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            StartOfGameRules.Declare(card,s=>{
                s.Controller[GameTag.MAXRESOURCES]+=5;
                s.Controller.Opponent[GameTag.MAXRESOURCES]+=5;
            });
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>Generic.ChangeManaCrystal(c,3,true)) };
        }
    }
}
