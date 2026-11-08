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
    internal static class LadyAzshara
    {
        private static OfflinePoolFilter Filter()=>OfflinePoolFilter.Random(type:CardType.SPELL);
        [OfflinePool("TIME_211",119928)]
        internal static PoolRequirement[] Pools()=>new[]{new PoolRequirement("well-spells",Filter(),new[]{"DRUID"})};
        [StandardCard("TIME_211",119928)]
        internal static void Configure(Card card) { card.Implemented=true; card.ExcludeFromLegacyRandomPools=true; card.Power=new Power(); }
        [StandardCard("TIME_211a",120204)]
        [StandardCard("TIME_211b",120205)]
        internal static void Option(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            string keep=card.Id=="TIME_211a"?"TIME_211t2":"TIME_211t1";
            string destroy=card.Id=="TIME_211a"?"TIME_211t1":"TIME_211t2";
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,k)=> {
                foreach(var location in c.HandZone.Concat(c.DeckZone).Concat(c.BoardZone.GetLocations()).ToArray())
                {
                    if(location.Card.Id==keep) Generic.ChangeEntityBlock(c,location,Cards.FromId(keep+"t"),true);
                    if(location.Card.Id==destroy || location.Card.Id==destroy+"t")
                    {
                        if(location.Zone==c.BoardZone) ((BoardObject)location).Destroy();
                        else c.GraveyardZone.Add(location.Zone.Remove(location));
                    }
                }
            }) };
        }
        [StandardCard("TIME_211t1",119929)]
        [StandardCard("TIME_211t1t",120200)]
        internal static void Well(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            bool empowered=card.Id=="TIME_211t1t";
            card.Power=new Power { LocationActivation=new LocationActivation { Task=new CustomTask((g,c,s,t,k)=> {
                var ctx=new EffectContext(g,c.Id,s.Id);
                int count=c.HandZone.MaxSize-c.HandZone.Count;
                for(int i=0;i<count;i++)
                {
                    var picked=ctx.PickRandomCard(Filter(),recipeDbfId:119928); if(picked==null) break;
                    var spell=Entity.FromCard(c,picked,new EntityData {[GameTag.CREATOR]=s.Id});
                    TemporaryRules.Make(spell);
                    if(empowered) RepeatRules.Mark(spell,(IPlayable)s,"TIME_211t1te");
                    Generic.AddHandPhase(c,spell);
                }
                s[GameTag.LOCATION_ACTION_COOLDOWN]=1;
            }) } };
        }
        [StandardCard("TIME_211t2",119930)]
        [StandardCard("TIME_211t2t",120202)]
        internal static void City(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            bool empowered=card.Id=="TIME_211t2t";
            card.Power=new Power { LocationActivation=new LocationActivation { Target=LocationTargetKind.Minion,
                TargetCondition=(c,t)=>t.Controller==c,RequiresFreeSlot=true,
                Task=new CustomTask((g,c,s,t,k)=> {
                    var copyStack=new TaskStack {Playables=new List<IPlayable>()};
                    new SummonCopyTask(EntityType.TARGET,addToStack:true).Process(g,c,s,t,copyStack);
                    if(empowered && copyStack.Playables.LastOrDefault() is Minion copy)
                        Generic.AddEnchantmentBlock(g,Cards.FromId("TIME_211t2te"),(IPlayable)s,copy,copy.AttackDamage,copy.Health);
                    s[GameTag.LOCATION_ACTION_COOLDOWN]=1;
                }) } };
        }
        [StandardCard("TIME_211t1te",120201)]
        internal static void Eternalized(Card card) { card.Implemented=true; card.Power=new Power(); }
        [StandardCard("TIME_211t2te",120203)]
        internal static void Strength(Card card) { card.Implemented=true; card.Power=new Power { Enchant=new Enchant(Effects.Attack_N(0),Effects.Health_N(0)) {UseScriptTag=true} }; }
    }
}
