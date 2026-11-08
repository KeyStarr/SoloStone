using System.Collections.Generic;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HarbingerOfWinter
    {
        internal const int DbfId = 81972;
        // Legacy CardDefs predates schools; Frost SPELL identities intersected with pinned 37.0.0.254477 export.
        private static readonly HashSet<int> LegacyFrostSpells = new HashSet<int> {
            172, 192, 430, 457, 519, 587, 621, 662, 971, 1907, 2089, 38407, 38989, 40324,
            42676, 42747, 42754, 42760, 42763, 43193, 43502, 47409, 47821, 51440, 51537,
            51538, 52255, 52639, 53174, 54193, 55248, 56168, 56171, 76273, 111365
        };
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = ComplexTask.DrawFromDeck(1, SelfCondition.IsSpell,
                new SelfCondition(p => p.Card[GameTag.SPELL_SCHOOL] == 3 || LegacyFrostSpells.Contains(p.Card.AssetId))) };
        }
    }
}
