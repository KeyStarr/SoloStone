using System;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TheLivingPlague
    {
        [StandardCard("JAIL_443", 126259)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true; card.Power=new Power();
        }
        internal static bool Replace(IPlayable source, Hero hero, int damage)
        {
            if(damage<=0 || source.Card.Id!="JAIL_443" || !(source is Minion minion) || minion.IsSilenced) return false;
            int count=Math.Min(damage,hero.Controller.DeckZone.FreeSpace);
            using var shuffle=source.Game.Facts.Shuffle(source.Controller.Id);
            using var presentation=source.Game.History && count>0 ? source.Game.PowerHistory.GeneratedShuffleSource(source) : null;
            int inserted=0;
            for(int i=0;i<count;i++)
                if(hero.Controller.DeckZone.AddGenerated(Cards.FromId("JAIL_443t"),source,shuffle:true)!=null) inserted++;
            if(inserted>0 && source.Game.History)
                source.Game.PowerHistory.Add(new PowerHistoryShuffleDeck { PlayerId=hero.Controller.PlayerId });
            return true;
        }
        [StandardCard("JAIL_443t", 129675)]
        internal static void Blight(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new DamageTask(2,EntityType.HERO), TopdeckTask=new CustomTask((g,c,s,t,stack)=>{
                Generic.DamageCharFunc((IPlayable)s,c.Hero,2,false);
                c.GraveyardZone.Add(c.SetasideZone.Remove((IPlayable)s));
                if(g.State==State.RUNNING && c.Hero.Health>0) Generic.Draw(c);
            }) };
        }
    }
}
