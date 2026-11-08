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
    internal static class SkittishSaucier
    {
        [StandardCard("DINO_137",118505)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;card.ConsumesHistory=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>{
                var play=g.HistoryFacts.Query.LatestPlay(s.Id);
                if(play==null || play.HandPosition<0) return;
                // Removal shifts the old right neighbor into the source's old slot.
                foreach(int index in new[]{play.HandPosition-1,play.HandPosition})
                    if(index>=0 && index<c.HandZone.Count) CostRules.AddModifier((Playable)c.HandZone[index],EffectOperator.SUB,1);
            }) };
        }
    }
}
