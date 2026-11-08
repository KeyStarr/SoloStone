using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MedivhTheHallowed
    {
        internal const int DbfId=120060;
        [StandardCard("TIME_890",DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power {
                Aura=new AdaptiveCostEffect(s=>0,EffectOperator.SET,
                    new SelfCondition(s=>s.Controller.BoardZone.GetLocations().Any(l=>l.Card.Id=="TIME_890t2"))),
                PowerTask=new CustomTask((g,c,s,t,stack)=> {
                    var others=c.BoardZone.Concat(c.Opponent.BoardZone).Where(m=>m.Id!=s.Id).ToArray();
                    foreach(var minion in others) minion.Silence();
                    foreach(var minion in others) minion.ToBeDestroyed=true;
                }) };
        }
        [StandardCard("TIME_890t",120064)]
        internal static void ConfigureAtiesh(Card card)
        {
            card.Implemented=true;
            card.Power=new Power {
                Aura=new AdaptiveCostEffect(s=>0,EffectOperator.SET,new SelfCondition(s=>s.Controller.BoardZone.Any(m=>m.Card.Id=="TIME_890"))),
                Trigger=new Trigger(TriggerType.EQUIP_WEAPON) { TriggerSource=TriggerSource.SELF,
                    SingleTask=new CustomTask((g,c,s,t,stack)=>new Aura(AuraType.CONTROLLER,
                        new Effect(GameTag.SPELLPOWER_DOUBLE,EffectOperator.ADD,1)).Activate((IPlayable)s)) } };
        }
        [StandardCard("TIME_890t2",120068)]
        internal static void ConfigureKarazhan(Card card)
        {
            card.Implemented=true;
            card.Power=new Power {
                Aura=new AdaptiveCostEffect(s=>0,EffectOperator.SET,new SelfCondition(s=>s.Controller.Hero.Weapon?.Card.Id=="TIME_890t")),
                LocationActivation=new LocationActivation { Target=LocationTargetKind.None,RequiresFreeSlot=true,
                    Task=new CustomTask((g,c,s,t,stack)=> {
                        var ctx=new EffectContext(g,c.Id,s.Id);
                        for(int i=0;i<2 && !c.BoardZone.IsFull;i++) {
                            var chosen=OfflineCardPools.Pick(g,CombatTimeTravelPools.EightCostMinion(),c.HeroClass,120068, PoolDestination.Summon);
                            if(chosen==null) break;
                            g.OnRandomHappened(true); ctx.Summon(chosen.Id);
                            g.DeathProcessingAndAuraUpdate();
                        }
                    }) } };
        }
    }
}
