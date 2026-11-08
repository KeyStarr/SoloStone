using System;
using System.Collections.Generic;
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
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HarbingerOfTheBlighted
    {
        [StandardCard("EDR_781",115176)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;card.ConsumesHistory=true;
            card.Power=new Power { Trigger=new Trigger(TriggerType.ZONE) { TriggerActivation=TriggerActivation.HAND,
                TriggerSource=TriggerSource.SELF,
                Condition=new SelfCondition(source=>source.Game.HistoryFacts.Query.LatestHandEntry(source.Id)?.FromZone==Zone.PLAY),
                SingleTask=new CustomTask((g,c,s,t,stack)=>{
                    var ctx=new EffectContext(g,c.Id,s.Id,stack:stack);
                    for(int i=0;i<2 && !c.BoardZone.IsFull;i++)
                    {
                        var chosen=ctx.PickRandomCard(EmeraldDreamHistoryExtPools.TwoCost(),recipeDbfId:115176);
                        if(chosen!=null)ctx.Summon(chosen.Id);
                    }
                }) } };
        }
    }
}
