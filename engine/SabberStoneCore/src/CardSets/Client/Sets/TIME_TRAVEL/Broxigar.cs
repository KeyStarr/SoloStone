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
    internal static class Broxigar
    {
        [StandardCard("TIME_020",120074)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            StartOfGameRules.Declare(card,FabledRules.Vanish,neverOpening:true);
        }
        [StandardCard("TIME_020t1",120082)]
        internal static void Axe(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Trigger=new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource=TriggerSource.HERO,
                Condition=new SelfCondition(p=>p.Game.CurrentEventData?.EventTarget is Minion victim && victim.ToBeDestroyed),
                SingleTask=new CustomTask((g,c,s,t,k)=>new EffectContext(g,c.Id,s.Id).DrawWhere(p=>
                    p.Card.Id=="TIME_020t2" || p.Card.Id=="TIME_020t3" || p.Card.Id=="TIME_020t4" || p.Card.Id=="TIME_020t5"))
            } };
        }
        [StandardCard("TIME_020t2",120083)]
        [StandardCard("TIME_020t3",120164)]
        [StandardCard("TIME_020t4",120166)]
        [StandardCard("TIME_020t5",120168)]
        internal static void Portal(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            string demon=card.Id+"t";
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,k)=> {
                if(c.Opponent.BoardZone.IsFull) return;
                var body=(Minion)Entity.FromCard(c.Opponent,Cards.FromId(demon),new EntityData {[GameTag.CREATOR]=s.Id});
                Generic.SummonBlock(g,body,-1,s);
            }) };
        }
        [StandardCard("TIME_020t2t",120142)]
        [StandardCard("TIME_020t3t",120165)]
        [StandardCard("TIME_020t4t",120167)]
        [StandardCard("TIME_020t5t",120169)]
        internal static void Demon(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.SetPrintedDeathrattle(); // The export omits 217 on all four printed Deathrattle Demons.
            int stage=card.Id[9]-'0';
            card.Power=new Power { DeathrattleTask=new CustomTask((g,c,s,t,k)=> {
                var recipient=c.Opponent;
                if(stage==5) { FabledRules.ReturnVanished(recipient,120074); return; }
                Generic.Draw(recipient);
                Generic.ShuffleIntoDeck(recipient,s,Entity.FromCard(recipient,Cards.FromId("TIME_020t"+(stage+1)),new EntityData {[GameTag.CREATOR]=s.Id}));
            }) };
        }
    }
}
