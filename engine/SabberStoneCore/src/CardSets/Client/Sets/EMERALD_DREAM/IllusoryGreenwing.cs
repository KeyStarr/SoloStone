using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class IllusoryGreenwing
    {
        [StandardCard("EDR_260", 114884)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { DeathrattleTask=new CustomTask((g,c,s,t,stack)=>{
                for(int i=0;i<2;i++) c.DeckZone.AddGenerated(Cards.FromId("EDR_260t"),s,shuffle:true);
            }) };
        }
        [StandardCard("EDR_260t", 115184)]
        internal static void Illusion(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { TopdeckTask=new CustomTask((g,c,s,t,stack)=>{
                var body=(Minion)s;
                if(body.Zone != c.HandZone) {
                    c.SetasideZone.Remove(body);
                    Generic.SummonBlock(g,body,c.BoardZone.SlotCount,s);
                }
                Generic.Draw(c);
            }) };
        }
    }
}
