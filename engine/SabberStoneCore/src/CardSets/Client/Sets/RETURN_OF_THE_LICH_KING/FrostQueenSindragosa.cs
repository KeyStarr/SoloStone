using System;
using System.Linq;
using System.Collections.Generic;
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
    internal static class FrostQueenSindragosa
    {
        [StandardCard("NX2_037", 87329)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.Declare(card, "NX2_037t", "NX2_037t2");
            card.Power = new Power { Trigger = new Trigger(TriggerType.FROZEN) {
                TriggerSource = TriggerSource.OP_MINIONS, FastExecution = true,
                SingleTask = new CustomTask((g,c,s,t,k) => { if (t is Minion m && m.Zone == c.Opponent.BoardZone && !m.Untouchable) m.Destroy(); }) } };
        }

        [StandardCard("NX2_037t", 87330)]
        [StandardCard("NX2_037t2", 94607)]
        internal static void Token(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}
