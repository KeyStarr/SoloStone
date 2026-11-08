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
    internal static class ScorchingWinds
    {
        internal const int DbfId = 115634;
        // Legacy CardDefs predates spell schools; identities pinned to 37.0.0.254477.
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
        internal static bool IsFireSpell(IPlayable p) => p is Spell && (p.Card[GameTag.SPELL_SCHOOL] == 2 || LegacyFireSpells.Contains(p.Card.AssetId));

        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                new DamageTask(3, EntityType.TARGET, true).Process(g, c, s, t, stack);
                var spells = c.HandZone.Where(IsFireSpell).ToArray();
                if (spells.Length == 0) return;
                var chosen = spells[g.Random.Next(spells.Length)];
                g.OnRandomHappened(true);
                Generic.DiscardBlock(c, chosen);
                if (t is ICharacter character && !character.IsDead && (t is Hero || t.Zone == t.Controller.BoardZone))
                    new DamageTask(3, EntityType.TARGET, true).Process(g, c, s, t, stack);
            }) };
        }
    }
}
