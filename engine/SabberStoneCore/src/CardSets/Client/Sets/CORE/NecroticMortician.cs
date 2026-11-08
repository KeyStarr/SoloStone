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
    internal static class NecroticMortician
    {
        [StandardCard("CORE_RLK_116",111328)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;card.ConsumesHistory=true;
            card.PoweredUpWhen(Condition);
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>{
                if(Condition((IPlayable)s)) new EffectContext(g,c.Id,s.Id,stack:stack).Discover(CoreHistoryExtPools.Unholy(),recipeDbfId:111328);
            }) };
        }
        private static bool Condition(IPlayable source)=>source.Game.HistoryFacts.Query.Deaths(
                source.Game.HistoryFacts.Query.SincePreviousTurnEnded(source.Controller.Id))
            .Any(f=>f.ControllerId==source.Controller.Id && (f.Races.Contains(Race.UNDEAD)||f.Races.Contains(Race.ALL)));
    }
}
