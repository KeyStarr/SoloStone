using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GaronaHalforcen
    {
        [StandardCard("TIME_875",119814)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            // Opponent hand identity must never drive a public glow.
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,k)=> {
                var llane=c.Opponent.HandZone.FirstOrDefault(p=>p.Card.Id=="TIME_875t");
                if(llane==null) return;
                c.Opponent.GraveyardZone.Add(c.Opponent.HandZone.Remove(llane));
                c.Opponent.Hero.Health=Math.Max(0,c.Opponent.Hero.Health/2);
            }) };
        }
        [StandardCard("TIME_875t",119815)]
        internal static void Llane(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            StartOfGameRules.Declare(card, source=> {
                source.Zone.Remove(source);
                source.Controller=source.Controller.Opponent;
                source[GameTag.CONTROLLER]=source.Controller.PlayerId;
                Generic.ShuffleIntoDeck(source.Controller,source,source);
            }, neverOpening:true);
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,k)=> {
                Generic.Draw(c);
                var llane=(Minion)s;
                if(llane.Zone!=c.BoardZone) return;
                c.BoardZone.Remove(llane); llane.Reset();
                Generic.ShuffleIntoDeck(c,s,llane);
            }) };
        }
        [StandardCard("TIME_875t1",119816)]
        internal static void Kingslayers(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Trigger=new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource=TriggerSource.HERO,
                SingleTask=new CustomTask((g,c,s,t,k)=> {
                    foreach(var owner in new[]{c,c.Opponent})
                        new EffectContext(g,owner.Id,s.Id).DrawWhere(p=>Entity.IsLegendary(p));
                })
            } };
        }
    }
}
