using System.Collections.Generic;
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
    internal static class FelfireBlaze
    {
        internal const int DbfId = 115628;
        // Legacy CardDefs predates spell schools; identities pinned to 37.0.0.254477.
        private static readonly HashSet<int> LegacyFelSpells = new HashSet<int> {
            1142, 2013, 2301, 2628, 40517, 46864, 50859, 52871, 53938, 54004, 54891, 54892,
            54893,
        };
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_CAST) {
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(p => p is Spell && (p.Card[GameTag.SPELL_SCHOOL] == 7 || LegacyFelSpells.Contains(p.Card.AssetId))),
                SingleTask = ComplexTask.Create(new DestroyTask(EntityType.SOURCE), new DamageTask(2, EntityType.ENEMIES))
            } };
        }
    }
}
