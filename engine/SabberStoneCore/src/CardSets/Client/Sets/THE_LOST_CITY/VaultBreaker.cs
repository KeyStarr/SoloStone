using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class VaultBreaker
    {
        [StandardCard("TLC_483", 120582)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            // Invoked only by semantic Discover completion, including direct cast/summon choices.
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (t is Playable chosen) CostRules.AddModifier(chosen, EffectOperator.SUB, 1);
            }) };
        }
    }
}
