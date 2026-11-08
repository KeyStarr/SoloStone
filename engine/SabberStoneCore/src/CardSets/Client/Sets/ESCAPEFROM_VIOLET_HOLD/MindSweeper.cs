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
    internal static class MindSweeper
    {
        [StandardCard("JAIL_432",126122)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(PayloadRules.HeldOpponentCopy);
            card.Power = new Power {
                HandEntryTask = new CustomTask((g,c,s,t,stack) => s[PayloadRules.HeldCopyPlayed] = 0),
                Trigger = PayloadRules.HoldingTrigger(),
                PowerTask = new CustomTask((g,c,s,t,stack) => {
                    if (PayloadRules.HeldOpponentCopy((IPlayable)s)) new DamageTask(2, EntityType.OP_MINIONS).Process(g,c,s,null,stack);
                })
            };
        }
    }
}
