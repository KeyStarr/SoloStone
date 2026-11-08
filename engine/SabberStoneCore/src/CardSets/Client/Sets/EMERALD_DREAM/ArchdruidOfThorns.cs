using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ArchdruidOfThorns
    {
        [StandardCard("EDR_491",115307)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>PayloadRules.GainDeathsThisTurn((IPlayable)s,"EDR_491e")) };
        }
        [StandardCard("EDR_491e",119206)]
        internal static void ConfigureGrant(Card card) { card.Implemented=true; card.Power=new Power(); }
    }
}
