using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enums;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LivingFlame
    {
        internal const int DbfId = 115655;
        // Legacy CardDefs predates spell schools; FIRE identities from pinned 37.0.0.254477 export.
        private static readonly HashSet<int> LegacyFireSpells = new HashSet<int> {
            286, 315, 394, 585, 864, 950, 974, 1004, 1087, 1640, 1927, 2284,
            2289, 2323, 2330, 2359, 2432, 2480, 2539, 2541, 2678, 2864, 2874, 2997,
            25271, 25396, 25401, 38413, 38935, 39715, 39747, 39854, 40005, 40066, 40297, 40316,
            40317, 40498, 40500, 40545, 40546, 40938, 41151, 41159, 41690, 41878, 43407, 43411,
            43412, 43414, 45271, 46286, 46425, 46437, 46715, 46953, 47251, 47414, 48393, 48824,
            49333, 50086, 50120, 50122, 51161, 51371, 51530, 52703, 53823, 54429, 55101, 55250,
            55276, 55858, 55859, 55860, 55861, 55935, 56170, 56173, 57316, 57483, 57484, 58970,
            69501, 69502,
        };
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = EffectContext.Task(ctx => ctx.RunLegacy(ComplexTask.DrawFromDeck(1, SelfCondition.IsSpell, new SelfCondition(me => me.Card[GameTag.SPELL_SCHOOL] == 2 || LegacyFireSpells.Contains(me.Card.AssetId))))) };
        }
    }
}
