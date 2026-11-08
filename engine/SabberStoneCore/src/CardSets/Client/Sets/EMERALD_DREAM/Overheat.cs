using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Overheat
    {
        internal const int DbfId = 115630;
        // Legacy CardDefs predates spell schools; identities pinned to 37.0.0.254477.
        private static readonly HashSet<int> LegacyNatureSpells = new HashSet<int> {
            51, 95, 137, 149, 151, 213, 233, 238, 239, 253, 254, 299,
            301, 325, 381, 451, 459, 493, 505, 629, 766, 767, 773, 808,
            836, 1124, 1802, 1809, 1832, 1925, 1976, 1995, 2001, 2006, 2009, 2041,
            2176, 2177, 2387, 2612, 2620, 2659, 2789, 2791, 2792, 2793, 25395, 25398,
            38262, 38266, 38289, 38329, 38652, 38654, 38655, 38724, 38934, 38938, 38940, 39712,
            39714, 40402, 40455, 40523, 40694, 40991, 41081, 41094, 41206, 41207, 41212, 41407,
            41521, 41834, 41879, 42011, 42168, 42648, 42656, 42665, 43282, 43288, 43289, 43290,
            43331, 43332, 43333, 43361, 43362, 43363, 45828, 45904, 46067, 46303, 47063, 47152,
            47222, 47553, 47887, 48051, 48161, 48524, 48607, 48798, 48819, 48820, 48821, 48837,
            48860, 48862, 49052, 49231, 49246, 49275, 49995, 49997, 50042, 50997, 51140, 51187,
            51373, 51769, 51773, 51775, 51779, 51781, 52148, 52421, 52819, 52821, 53576, 53578,
            53579, 53978, 53979, 54001, 54128, 54160, 54400, 54405, 54833, 54922, 54965, 54971,
            55035, 55036, 55039, 55040, 55041, 55052, 55778, 55779, 55780, 55784, 55789, 55790,
            55791, 57587, 57993, 57994,
        };
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                new AddEnchantmentTask("FIR_906e", EntityType.MINIONS).Process(g, c, s, null, stack);
                var spells = c.HandZone.Where(p => p is Spell && (p.Card[GameTag.SPELL_SCHOOL] == 4 || LegacyNatureSpells.Contains(p.Card.AssetId))).ToArray();
                if (spells.Length == 0) return;
                var chosen = spells[g.Random.Next(spells.Length)];
                g.OnRandomHappened(true);
                Generic.DiscardBlock(c, chosen);
                new AddEnchantmentTask("FIR_906e", EntityType.MINIONS).Process(g, c, s, null, stack);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) };
        }
    }
}
