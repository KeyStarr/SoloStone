using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CrystalsmithCultist
    {
        internal const int DbfId = 129166;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HoldingShadowSpell);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (HoldingShadowSpell((IPlayable)s))
                    new AddEnchantmentTask("RLK_814e", EntityType.SOURCE).Process(g, c, s, t, stack);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) };
        }

        // Legacy CardDefs predates spell schools; identities pinned to 37.0.0.254477.
        private static readonly HashSet<int> LegacyShadowSpells = new HashSet<int> {
            8, 23, 30, 145, 147, 163, 217, 220, 365, 545, 846, 859, 860, 914,
            919, 982, 990, 1092, 1099, 1100, 1189, 1363, 1367, 1368, 1850, 1855, 1864, 1868,
            1985, 2093, 2535, 2564, 2632, 2879, 2998, 2999, 9082, 38426, 38433, 38434, 38439, 38454,
            38456, 38578, 38770, 38942, 39663, 39664, 40326, 40327, 40373, 40383, 40384, 40385, 40386, 40393,
            40560, 40936, 40957, 41169, 41871, 41872, 41942, 42391, 42469, 42471, 42479, 42565, 42566, 42586,
            42587, 42590, 42597, 42649, 42658, 42804, 42992, 43112, 43128, 43272, 43275, 43276, 45308, 45337,
            45345, 45353, 45384, 45387, 45650, 45654, 45724, 45756, 45979, 46034, 46035, 46037, 46038, 46039,
            46194, 46295, 46296, 46297, 46412, 46413, 46956, 47033, 47040, 47054, 47411, 47412, 47413, 47415,
            47555, 48002, 48035, 48113, 48211, 48320, 48329, 48387, 48389, 48391, 48748, 48823, 48957, 49249,
            49423, 49802, 49853, 50002, 50004, 50012, 50071, 50278, 50282, 50432, 50455, 50991, 51148, 51151,
            51376, 51847, 51966, 52263, 52632, 53442, 54016, 54017, 54298, 54426, 54431, 54873, 55125, 57532,
            57628, 57969,
        };
        internal static bool IsLegacyShadowSpell(int dbfId) => LegacyShadowSpells.Contains(dbfId);

        private static bool HoldingShadowSpell(IPlayable source) =>
            source.Controller.HandZone.Any(p => p.Id != source.Id && p is Spell
                && (p.Card[GameTag.SPELL_SCHOOL] == 6 || IsLegacyShadowSpell(p.Card.AssetId)));

    }
}
