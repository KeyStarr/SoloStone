using System.Collections.Generic;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class VeteranWarmedic
    {
        internal const int DbfId = 130580;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_CAST) {
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(IsHoly),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    int position = s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                    new EffectContext(g, c.Id, s.Id, stack: stack).Summon("BAR_878t", position);
                })
            } };
        }

        internal static bool IsHoly(IPlayable p) => p is Spell &&
            (p.Card[GameTag.SPELL_SCHOOL] == 5 || LegacyHolySpells.Contains(p.Card.AssetId));

        // Legacy CardDefs predates spell schools; identities pinned to 37.0.0.254477.
        private static readonly HashSet<int> LegacyHolySpells = new HashSet<int> {
            70, 140, 232, 250, 279, 291, 435, 462, 476, 594, 613, 679, 727, 756,
            841, 943, 1174, 1361, 1362, 1365, 1366, 1373, 1522, 1804, 1933, 1935, 1938, 2025,
            2298, 2568, 2582, 2717, 2899, 25397, 38461, 38666, 38684, 38749, 38913, 39468, 39716, 40375,
            41170, 42824, 43373, 43380, 43381, 43382, 45751, 45753, 45757, 45759, 46306, 46307, 46308, 46694,
            47549, 48354, 48378, 49237, 50028, 50794, 50973, 52490, 52633, 53827, 53971, 54189, 54425, 54834,
            54865, 55787,
        };
    }
}
