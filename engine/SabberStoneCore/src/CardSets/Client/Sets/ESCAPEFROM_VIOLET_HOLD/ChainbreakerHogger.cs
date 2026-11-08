using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ChainbreakerHogger
    {
        [StandardCard("JAIL_384", 126002)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power();
            StartOfGameRules.Declare(card,s=>{
                var c=s.Controller; var g=c.Game;
                var eligible=c.DeckZone.Concat(c.HandZone).Where(e=>e.Id!=s.Id && Entity.IsLegendary(e)).ToArray();
                for(int i=0;i<eligible.Length-1;i++) {
                    int j=g.Random.Next(i,eligible.Length);
                    var swap=eligible[i]; eligible[i]=eligible[j]; eligible[j]=swap;
                }
                foreach(var other in eligible) {
                    if(c.DeckZone.IsFull) break;
                    c.DeckZone.AddGenerated(other.Card,s,shuffle:true);
                }
            },priority:int.MaxValue);
        }
    }
}
