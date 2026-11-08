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
    internal static class ProtoPandaren
    {
        [StandardCard("BE_997",132135)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;card.ConsumesHistory=true;
            card.Power=new Power { Trigger=new Trigger(TriggerType.ZONE) { TriggerSource=TriggerSource.FRIENDLY,
                Condition=new SelfCondition(p=>p.Zone==p.Controller.HandZone),
                SingleTask=new CustomTask((g,c,s,t,stack)=>{
                    if(t==null || g.HistoryFacts.Query.LatestHandEntry(t.Id)?.SourceId==s.Id) return;
                    var chosen=new EffectContext(g,c.Id,s.Id,stack:stack).PickRandomCard(BEHistoryExtPools.Spell(),recipeDbfId:132135);
                    if(chosen==null) return;
                    new EffectContext(g,c.Id,s.Id).AddToHand(chosen.Id);
                    new EffectContext(g,c.Opponent.Id,s.Id).AddToHand(chosen.Id);
                }) } };
        }
    }
}
