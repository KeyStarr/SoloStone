using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TomeTranslator
    {
        private const GameTag Selected = TurnQueueRules.TomeSelection;
        private static readonly string[] Forms = { "BE_327t", "BE_327t1", "BE_327t2", "BE_327t3" };
        internal static OfflinePoolFilter Pool => OfflinePoolFilter.Random(type:CardType.MINION,minCost:2,maxCost:2,keyword:GameTag.DEATHRATTLE);
        [OfflinePool("BE_327",129235)]
        internal static PoolRequirement[] Pools() => new[]{new PoolRequirement("two-cost-deathrattle",Pool,new[]{"NEUTRAL"}, destination: PoolDestination.Summon)};
        [OfflinePool("BE_327t",129236)]
        internal static PoolRequirement[] FormPools() => Pools();
        [StandardCard("BE_327",129235)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power {
                HandEntryTask=new CustomTask((g,c,s,t,stack)=>{
                    s[Selected]=Cards.FromId(Forms[g.Random.Next(Forms.Length)]).AssetId;
                    s[GameTag.TAG_SCRIPT_DATA_NUM_1]=2;
                    TurnQueueRules.Arm(s,c,2);
                }),
                Trigger=new Trigger(TriggerType.TURN_START) { TriggerActivation=TriggerActivation.HAND,
                    SingleTask=new CustomTask((g,c,s,t,stack)=>{
                        if (s.Zone!=c.HandZone) return;
                        bool due=TurnQueueRules.Tick(s);
                        s[GameTag.TAG_SCRIPT_DATA_NUM_1]=s[(GameTag)32801];
                        if(due) Reveal((IPlayable)s);
                    }) },
                PowerTask=new CustomTask((g,c,s,t,stack)=>{
                    var revealed=Reveal((IPlayable)s);
                    revealed.Power.PowerTask.Process(g,c,revealed,t,stack);
                }) };
        }
        private static IPlayable Reveal(IPlayable source)
        {
            int selected=source[Selected];
            if(selected==0) selected=Cards.FromId(Forms[source.Game.Random.Next(Forms.Length)]).AssetId;
            source[Selected]=0;source[GameTag.TAG_SCRIPT_DATA_NUM_1]=0;
            return Generic.ChangeEntityBlock(source.Controller,source,Cards.FromAssetId(selected),false);
        }
        [StandardCard("BE_327t",129236)]
        internal static void Summons(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>{
                var context=new EffectContext(g,c.Id,s.Id);
                for(int i=0;i<2;i++)
                {
                    var chosen=context.PickRandomCard(Pool,recipeDbfId:129236);
                    if(chosen!=null) context.Summon(chosen.Id);
                }
            }) };
        }
        [StandardCard("BE_327t1",131120)]
        internal static void Discount(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>
                Generic.AddEnchantmentBlock(g,Cards.FromId("BE_327te"),(IPlayable)s,c)) };
        }
        [StandardCard("BE_327te",134937)]
        internal static void DiscountActor(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Aura=new Aura(AuraType.HAND,Effects.ReduceCost(4)) {
                Condition=SelfCondition.IsSpell, RemoveTrigger=(TriggerType.CAST_SPELL,null) } };
        }
        [StandardCard("BE_327t2",129237)]
        internal static void Missiles(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>{
                for(int i=0;i<6;i++)
                {
                    var targets=c.BoardZone.Concat(c.Opponent.BoardZone).Where(m=>m.Id!=s.Id && !m.IsDormant && !m.ToBeDestroyed).ToArray();
                    if(targets.Length==0) break;
                    Generic.DamageCharFunc((IPlayable)s,targets[g.Random.Next(targets.Length)],1,false);
                    g.DeathProcessingAndAuraUpdate();
                }
            }) };
        }
        [StandardCard("BE_327t3",131121)]
        internal static void Stats(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>{
                var targets=c.BoardZone.Where(m=>m.Id!=s.Id && !m.IsDormant && System.Math.Abs(m.ZonePosition-((IPlayable)s).ZonePosition)==1).ToArray();
                if(targets.Length==0) return;
                var chosen=targets[g.Random.Next(targets.Length)];
                Generic.AddEnchantmentBlock(g,Cards.FromId("BE_327te2"),(IPlayable)s,s,chosen.AttackDamage,chosen.Health);
            }) };
        }
        [StandardCard("BE_327te2",134938)]
        internal static void StatsActor(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Enchant=new Enchant(Effects.Attack_N(0),Effects.Health_N(0)){UseScriptTag=true} };
        }
    }
}
