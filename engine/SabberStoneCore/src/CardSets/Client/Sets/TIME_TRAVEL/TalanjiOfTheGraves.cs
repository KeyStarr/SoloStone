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
    internal static class TalanjiOfTheGraves
    {
        private static readonly string[] Boons={"TIME_619e","TIME_619e2","TIME_619e3"};
        private static readonly string[] Pacts={"TIME_619e4","TIME_619e5","TIME_619e6"};
        private static readonly GameTag[] Keywords={GameTag.TAUNT,GameTag.LIFESTEAL,GameTag.RUSH};
        private static OfflinePoolFilter Filter()=>OfflinePoolFilter.Random(type:CardType.MINION);
        [OfflinePool("TIME_619",120183)]
        internal static PoolRequirement[] Pools()=>new[]{new PoolRequirement("bwonsamdi-exact-cost",Filter(),new[]{"DEATHKNIGHT"},PoolCostBinding.StackNumber,ordinaryCost:4,allowThinExactCost:true, destination: PoolDestination.Summon)};
        private static int Cost(Controller c)=>4+2*(c.AppliedEnchantments?.Count(e=>Boons.Contains(e.Card.Id))??0);
        private static int DisplayCost(IPlayable p)=>Cost(p.Controller);
        private static ISimpleTask[] Choice()=>new ISimpleTask[] {
            new DiscoverThenContinueTask("TIME_619t3","TIME_619t4","TIME_619t5"),
            new CustomTask((g,c,s,t,k)=> {
                var picked=k.Playables.FirstOrDefault(); if(picked==null) return;
                int index=picked.Card.Id.Last()-'3';
                Generic.AddEnchantmentBlock(g,Cards.FromId(Boons[index]),(IPlayable)s,c);
            }) };
        [StandardCard("TIME_619",120183)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true; card.ConsumesHistory=true;
            var start=new CustomTask((g,c,s,t,k)=> {
                var ctx=new EffectContext(g,c.Id,s.Id);
                if(g.HistoryFacts.Query.Deaths().Any(f=>f.ControllerId==c.Id && f.DefinitionKnown && f.Definition.ReadDefinition().Id=="TIME_619t"))
                    ctx.Summon("TIME_619t");
                else ctx.DrawWhere(p=>p.Card.Id=="TIME_619t");
            });
            card.Power=new Power { PowerTask=new DecisionBattlecryTask("client/TIME_619/v1",new ISimpleTask[]{start}.Concat(Choice()).ToArray()) };
        }
        [StandardCard("TIME_619t",120187)]
        internal static void Bwonsamdi(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1,DisplayCost);
            card.BoardHistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1,DisplayCost);
            card.Power=new Power { DeathrattleTask=new CustomTask((g,c,s,t,k)=> {
                var ctx=new EffectContext(g,c.Id,s.Id,stack:new TaskStack {Number=Cost(c)});
                var chosen=ctx.PickRandomCard(Filter(),PoolCostBinding.StackNumber,120183);
                if(chosen==null || c.BoardZone.IsFull) return;
                var body=(Minion)Entity.FromCard(c,chosen,new EntityData {[GameTag.CREATOR]=s.Id});
                for(int i=0;i<3;i++)
                    if(c.AppliedEnchantments?.Any(e=>e.Card.Id==Boons[i])==true)
                        Generic.AddEnchantmentBlock(g,Cards.FromId(Pacts[i]),(IPlayable)s,body);
                Generic.SummonBlock(g,body,EffectContext.DeathrattlePosition(c,s),s);
            }) };
        }
        [StandardCard("TIME_619t2",120188)]
        internal static void Zandalar(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new DecisionSpellTask("client/TIME_619t2/v1",
                new ISimpleTask[]{new DamageTask(2,EntityType.ENEMIES,true)}.Concat(Choice()).ToArray()) };
        }
        [StandardCard("TIME_619t3",120184)]
        [StandardCard("TIME_619t4",120185)]
        [StandardCard("TIME_619t5",120186)]
        internal static void Option(Card card) { card.Implemented=true; card.ExcludeFromLegacyRandomPools=true; }
        [StandardCard("TIME_619e",120189)]
        [StandardCard("TIME_619e2",120190)]
        [StandardCard("TIME_619e3",120191)]
        internal static void PlayerBoon(Card card)
        {
            card.Implemented=true; int index=Array.IndexOf(Boons,card.Id);
            card.Power=new Power { Aura=new Aura(AuraType.HAND_AND_BOARD,new Effect(Keywords[index],EffectOperator.ADD,1)) {
                Condition=new SelfCondition(p=>p.Card.Id=="TIME_619t")
            } };
        }
        [StandardCard("TIME_619e4",120192)]
        [StandardCard("TIME_619e5",120193)]
        [StandardCard("TIME_619e6",120194)]
        internal static void Pact(Card card)
        {
            card.Implemented=true; int index=Array.IndexOf(Pacts,card.Id);
            card.Power=new Power { Enchant=new Enchant(new Effect(Keywords[index],EffectOperator.SET,1)) };
        }
    }
}
