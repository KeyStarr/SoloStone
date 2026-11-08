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
    internal static class PreciseShot
    {
        [StandardCard("TIME_600",119696)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;card.ConsumesHistory=true;
            card.PoweredUpWhen(Centered);
            card.SetPlayRequirements(new Dictionary<PlayReq,int>{{PlayReq.REQ_TARGET_TO_PLAY,0}});
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>{
                var play=g.HistoryFacts.Query.LatestPlay(s.Id);
                bool centered=g.Facts.PlayingEntity==s.Id && play!=null && play.HandCount%2==1 && play.HandPosition==play.HandCount/2;
                if(t is ICharacter target) Generic.DamageCharFunc((IPlayable)s,target,centered?5:3,true);
            }) };
        }
        private static bool Centered(IPlayable source)=>source.Controller.HandZone.Count%2==1
            && source.ZonePosition==source.Controller.HandZone.Count/2;
    }
}
