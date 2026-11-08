using System;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DevouringPlague
    {
        internal const int DbfId = 130582;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int missiles = (4 + SabberStoneCore.Actions.SpellDamageRules.Bonus((SabberStoneCore.Model.Entities.IPlayable)s))
                    * (int)Math.Pow(2, c.ControllerAuraEffects[GameTag.SPELLPOWER_DOUBLE]);
                SplitHits.Inline(g, c, s, missiles,
                    ComplexTask.DamageRandomTargets(1, EntityType.OP_MINIONS, 1), deathProcessing: true);
            }) };
        }
    }
}
