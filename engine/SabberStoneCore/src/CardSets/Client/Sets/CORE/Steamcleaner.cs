using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Steamcleaner
    {
        [StandardCard("CORE_REV_946", 118163)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>{
                // shortcut: preserve unknown fair-view origins; add deck-origin beliefs if bot evaluation needs them.
                foreach(var owner in new[]{c,c.Opponent})
                    foreach(var entity in owner.DeckZone.Where(e=>g.HistoryFacts.Origin(e.Id,owner.Id)==false).ToArray())
                        owner.GraveyardZone.Add(owner.DeckZone.Remove(entity));
            }) };
        }
    }
}
