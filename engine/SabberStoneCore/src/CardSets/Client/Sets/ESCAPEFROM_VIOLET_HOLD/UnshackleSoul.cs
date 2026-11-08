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
    internal static class UnshackleSoul
    {
        [StandardCard("JAIL_433",126124)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new System.Collections.Generic.Dictionary<PlayReq,int> {
                { PlayReq.REQ_TARGET_TO_PLAY,0 }, { PlayReq.REQ_MINION_TARGET,0 }
            });
            card.PoweredUpWhen(PayloadRules.HeldOpponentCopy);
            card.Power = new Power {
                HandEntryTask = new CustomTask((g,c,s,t,stack) => s[PayloadRules.HeldCopyPlayed] = 0),
                Trigger = PayloadRules.HoldingTrigger(),
                Aura = new AdaptiveCostEffect(p => 1, EffectOperator.SET, new SelfCondition(PayloadRules.HeldOpponentCopy)),
                PowerTask = new DestroyTask(EntityType.TARGET)
            };
        }
    }
}
