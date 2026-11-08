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
    internal static class NeptulonTheTidehunter
    {
        [StandardCard("TID_712", 75583)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.Declare(card, "TID_712t", "TID_712t2");
            card.Power = new Power();
        }

        [StandardCard("TID_712t", 75584)]
        [StandardCard("TID_712t2", 80016)]
        internal static void Token(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Tags[GameTag.IMMUNE_WHILE_ATTACKING] = 1; card.Power = new Power();
        }
    }
}
