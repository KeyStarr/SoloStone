using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PrimalRockfang
    {
        [StandardCard("BE_731",130764)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=> {
                if(s is Minion minion && minion.Zone==c.BoardZone)
                    foreach(var neighbor in minion.GetAdjacentMinions())
                        PayloadRules.GainDeathrattles(minion,neighbor,"BE_731e");
            }) };
        }
        [StandardCard("BE_731e",133443)]
        internal static void ConfigureGrant(Card card) { card.Implemented=true; card.Power=new Power(); }
    }
}
