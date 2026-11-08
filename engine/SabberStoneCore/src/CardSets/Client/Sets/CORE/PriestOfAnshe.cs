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
    internal static class PriestOfAnshe
    {
        [StandardCard("CORE_BAR_313",129172)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;card.ConsumesHistory=true;
            card.PoweredUpWhen(Condition);
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>{
                if(Condition((IPlayable)s)) Generic.AddEnchantmentBlock(g,Cards.FromId("BAR_313e"),(IPlayable)s,s);
            }) };
        }
        private static bool Condition(IPlayable source)=>HealingRules.RestoredHealthThisTurn(source.Controller);
        [StandardCard("BAR_313e",62882)]
        internal static void Buff(Card card)
        { card.Implemented=true;card.Power=new Power { Enchant=new Enchant(Effects.AttackHealth_N(3)) }; }
    }
}
