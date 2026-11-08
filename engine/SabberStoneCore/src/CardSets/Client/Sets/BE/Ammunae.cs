using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Ammunae
    {
        [StandardCard("BE_852",129842)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("BE_852e", EntityType.CONTROLLER) };
        }
        [StandardCard("BE_852e",133123)]
        internal static void ConfigureEffect(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION) {
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = new CustomTask((g,c,s,t,stack) => PayloadRules.CopyAfterPlay(s,t))
            } };
        }
    }
}
