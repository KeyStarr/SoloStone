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
    internal static class CrystalspineCub
    {
        [StandardCard("CATA_130",122966)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Trigger=new Trigger(TriggerType.LAST_MANA_SPENT) {
                TriggerSource=TriggerSource.FRIENDLY, SingleTask=new AddEnchantmentTask("CATA_130e",EntityType.SOURCE) } };
        }
        [StandardCard("CATA_130e",122980)]
        internal static void Buff(Card card)
        { card.Implemented=true;card.Power=new Power { Enchant=new Enchant(Effects.AttackHealth_N(1)) }; }
    }
}
