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
    internal static class TimethiefRafaam
    {
        private static bool Family(Card c)=>c.Id=="TIME_005" || FabledRules.Bundle("TIME_005").Contains(c.Id) || c.Id=="TIME_005t9t";
        private static int Remaining(IPlayable p)=>9-p.Game.HistoryFacts.Query.Plays().Where(f=>f.ControllerId==p.Controller.Id && f.DefinitionKnown)
            .Select(f=>f.Definition.ReadDefinition().Id).Where(id=>FabledRules.Bundle("TIME_005").Contains(id)).Distinct().Count();
        private static bool Ready(IPlayable p)=>Remaining(p)==0;
        // SDN1 is progress (ENTITY_TAG_THRESHOLD 2459=2, 2460=9 -> client prints 9-SDN1 left, Ready at >= 9), not remaining.
        private static int Progress(IPlayable p)=>9-Remaining(p);
        private static bool HoldingOther(IPlayable p)=>p.Controller.HandZone.Any(other=>other.Id!=p.Id && Family(other.Card));
        private static int PlayedFamily(IPlayable p)=>p.Game.HistoryFacts.Query.Plays().Count(f=>f.ControllerId==p.Controller.Id && f.DefinitionKnown && Family(f.Definition.ReadDefinition()));
        private static bool HasPlayedFamily(IPlayable p)=>PlayedFamily(p)>0;
        [StandardCard("TIME_005",119432)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true; card.ConsumesHistory=true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1,Progress);
            PoweredUp.Register(card,Ready);
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,k)=> { if(Remaining((IPlayable)s)==0) c.Opponent.Hero.Destroy(); }) };
        }
        [StandardCard("TIME_005t1",119433)]
        internal static void Tiny(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            var draw=new CustomTask((g,c,s,t,k)=>new EffectContext(g,c.Id,s.Id).DrawWhere(p=>Family(p.Card)));
            card.Power=new Power { PowerTask=draw,DeathrattleTask=draw };
        }
        [StandardCard("TIME_005t2",119434)]
        internal static void Green(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,k)=> {
                foreach(var rafaam in c.HandZone.Concat(c.BoardZone).Where(p=>p.Id!=s.Id && Family(p.Card)).ToArray())
                    Generic.AddEnchantmentBlock(g,Cards.FromId("TIME_005t2e"),(IPlayable)s,rafaam);
            }) };
        }
        [StandardCard("TIME_005t2e",119440)]
        internal static void Buff(Card card) { card.Implemented=true; card.Power=new Power { Enchant=new Enchant(Effects.AttackHealth_N(2)) }; }
        [StandardCard("TIME_005t3",119441)]
        internal static void Explorer(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new DecisionBattlecryTask("client/TIME_005t3/v1",
                new DiscoverThenContinueTask((g,c,s,k)=>c.DeckZone.Where(p=>Family(p.Card)).Select(p=>p.Card).Distinct().ToArray().ChooseNElements(3,g.Random)),
                new CustomTask((g,c,s,t,k)=> {
                    var chosen=k.Playables.FirstOrDefault(); if(chosen==null) return;
                    new EffectContext(g,c.Id,s.Id).DrawWhere(p=>p.Card.Id==chosen.Card.Id);
                })) };
        }
        [StandardCard("TIME_005t4",119443)]
        internal static void Warchief(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            PoweredUp.Register(card,HoldingOther);
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,k)=> {
                using (g.History ? g.PowerHistory.Visual(g,Presentation.VisualPhase.Armor,(IPlayable)s,c.Hero) : null)
                    c.Hero.Armor+=HoldingOther((IPlayable)s)?10:5;
            }) };
        }
        [StandardCard("TIME_005t5",119444)]
        internal static void Mindflayer(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            PoweredUp.Register(card,HoldingOther);
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,k)=> {
                if(HoldingOther((IPlayable)s)) new SummonCopyTask(EntityType.SOURCE).Process(g,c,s,t,k);
            }) };
        }
        [StandardCard("TIME_005t6",119445)]
        internal static void Calamitous(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,k)=> {
                var victims=c.BoardZone.Concat(c.Opponent.BoardZone).Where(p=>!Family(p.Card)).ToArray();
                using (g.History ? g.PowerHistory.DamageWave(victims.Length) : null)
                using (g.History ? g.PowerHistory.Visual(g,Presentation.VisualPhase.DamageGroup,(IPlayable)s) : null)
                    foreach(var victim in victims)
                        victim.TakeDamage((IPlayable)s,6);
            }) };
        }
        [StandardCard("TIME_005t7",119446)]
        internal static void Giant(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true; card.ConsumesHistory=true;
            PoweredUp.Register(card,HasPlayedFamily);
            card.Power=new Power { Aura=new AdaptiveCostEffect(PlayedFamily) };
        }
        [StandardCard("TIME_005t8",119447)]
        internal static void Murloc(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new AddEnchantmentTask("TIME_005t8e",EntityType.CONTROLLER) };
        }
        [StandardCard("TIME_005t8e",119448)]
        internal static void Discount(Card card)
        {
            card.Implemented=true;
            card.Power=new Power {
                Aura=new Aura(AuraType.HAND,Effects.ReduceCost(3)) { Condition=new SelfCondition(p=>Family(p.Card)) },
                Trigger=new Trigger(TriggerType.PLAY_CARD) { TriggerSource=TriggerSource.FRIENDLY,
                    Condition=new SelfCondition(p=>Family(p.Card)),SingleTask=RemoveEnchantmentTask.Task }
            };
        }
        [StandardCard("TIME_005t9",119450)]
        internal static void Archmage(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,k)=> {
                var ctx=new EffectContext(g,c.Id,s.Id);
                foreach(var victim in c.BoardZone.Concat(c.Opponent.BoardZone).Where(p=>!Family(p.Card)).ToArray()) ctx.Transform(victim.Id,"TIME_005t9t");
            }) };
        }
        [StandardCard("TIME_005t9t",119562)]
        internal static void Sheep(Card card) { card.Implemented=true; card.ExcludeFromLegacyRandomPools=true; }
    }
}
