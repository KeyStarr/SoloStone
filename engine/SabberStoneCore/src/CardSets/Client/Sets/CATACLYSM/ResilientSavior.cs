using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ResilientSavior
    {
        [StandardCard("MEND_801", 123621)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.LOSE_DIVINE_SHIELD) {
                TriggerSource = TriggerSource.SELF,
                SingleTask = new CustomTask((g,c,s,t,stack) => {
                    if (s is Minion minion && !minion.IsSilenced) LifecycleRules.StrengthenRecruits(minion);
                })
            } };
        }
        [StandardCard("MEND_801e", 123628)]
        internal static void ConfigureLedger(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
        [StandardCard("MEND_801e2", 123630)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Health_N(1)) };
        }
    }
}
