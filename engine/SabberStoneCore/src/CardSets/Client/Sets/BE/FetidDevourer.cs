using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FetidDevourer
    {
        internal const int DbfId = 131127;
        [StandardCard("BE_781", DbfId)]
        internal static void Configure(Card card) => ConfigureForm(card);
        [StandardCard("BE_781t1", 131169)]
        [StandardCard("BE_781t2", 131170)]
        [StandardCard("BE_781t3", 131171)]
        [StandardCard("BE_781t4", 132565)]
        [StandardCard("BE_781t5", 132566)]
        [StandardCard("BE_781t6", 132567)]
        [StandardCard("BE_781t7", 132568)]
        [StandardCard("BE_781t8", 132571)]
        [StandardCard("BE_781t9", 132573)]
        [StandardCard("BE_781t10", 132574)]
        [StandardCard("BE_781t11", 132700)]
        [StandardCard("BE_781t12", 132701)]
        [StandardCard("BE_781t13", 132702)]
        [StandardCard("BE_781t14", 132703)]
        [StandardCard("BE_781t15", 132704)]
        [StandardCard("BE_781t16", 132705)]
        [StandardCard("BE_781t17", 132706)]
        [StandardCard("BE_781t18", 132707)]
        [StandardCard("BE_781t19", 132708)]
        internal static void ConfigureForm(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.SetPlayRequirements(new Dictionary<PlayReq,int>());
            int blood=card[GameTag.COST_BLOOD],frost=card[GameTag.COST_FROST],unholy=card[GameTag.COST_UNHOLY];
            var steps=new List<ISimpleTask>();
            for(int i=0;i<blood;i++)
            {
                // Unpinned_FetidBattlecryChoices: rebuild each offer after deaths, before draws/summons.
                steps.Add(new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Board,SelectorSide.Enemy,cardType:CardType.MINION)));
                steps.Add(EffectContext.Task(ctx=>{
                    if(ctx.LastChosenEntityId!=0)ctx.RunLegacy(new CustomTask((g,c,s,t,stack)=> { t.Destroy(); g.DeathProcessingAndAuraUpdate(); }),ctx.LastChosenEntityId);
                    return TaskState.COMPLETE;
                }));
            }
            if(frost>0)steps.Add(new CustomTask((g,c,s,t,stack)=>{
                for(int i=0;i<frost;i++)
                {
                    var drawn=Generic.Draw(c);
                    if(drawn!=null && drawn.Zone==c.HandZone)drawn.Cost-=3;
                }
            }));
            if(unholy>0)steps.Add(new CustomTask((g,c,s,t,stack)=>{
                var ctx=new EffectContext(g,c.Id,s.Id,stack:new TaskStack {Number=3*unholy});
                for(int i=0;i<2;i++)
                {
                    if(c.BoardZone.IsFull)continue;
                    var chosen=ctx.PickRandomCard(B14H4BEPools.Minion(),PoolCostBinding.StackNumber,DbfId);
                    if(chosen!=null)ctx.Summon(chosen.Id,s.Zone==c.BoardZone?((IPlayable)s).ZonePosition+1+i:-1);
                }
            }));
            card.Power=new Power {
                PowerTask=steps.Count==0?null:new DecisionBattlecryTask("client/"+card.Id+"/h4-v1",steps.ToArray()),
                Trigger=new Trigger(TriggerType.AFTER_PLAY_CARD) {
                    TriggerActivation=TriggerActivation.HAND,TriggerSource=TriggerSource.FRIENDLY,
                    Condition=new SelfCondition(p=>p.Card[GameTag.COST_BLOOD]+p.Card[GameTag.COST_FROST]+p.Card[GameTag.COST_UNHOLY]>0),
                    SingleTask=new CustomTask((g,c,s,t,stack)=>{
                        if(!(s is Minion minion) || minion.Zone!=c.HandZone || t==null)return;
                        int b=t.Card[GameTag.COST_BLOOD],f=t.Card[GameTag.COST_FROST],u=t.Card[GameTag.COST_UNHOLY];
                        var target=Forms.Select(Cards.FromId).SingleOrDefault(x=>x[GameTag.COST_BLOOD]==b && x[GameTag.COST_FROST]==f && x[GameTag.COST_UNHOLY]==u);
                        if(target==null)return;
                        // Unpinned transition policy: keep identity/slot, discard outside attachments and reset live costs/stats.
                        using var handBuff=g.History?g.PowerHistory.HandBuff(minion):null;
                        Generic.ChangeEntityBlock(c,minion,target,true);
                    }) }
            };
        }
        private static readonly string[] Forms = { "BE_781t1", "BE_781t2", "BE_781t3", "BE_781t4", "BE_781t5", "BE_781t6", "BE_781t7", "BE_781t8", "BE_781t9", "BE_781t10", "BE_781t11", "BE_781t12", "BE_781t13", "BE_781t14", "BE_781t15", "BE_781t16", "BE_781t17", "BE_781t18", "BE_781t19" };
    }
}
