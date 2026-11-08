using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PrimusofPower
    {
        internal const int DbfId = 131056;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(BEHighlightConditions.PlayedTwoSpellSchools);
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_CAST) {
                TriggerActivation = TriggerActivation.HAND,
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (s.Zone != c.HandZone || !(t is Spell)) return;
                    int school = t.Card[GameTag.SPELL_SCHOOL];
                    if (school <= 0) return;
                    int mask = s[GameTag.TAG_SCRIPT_DATA_NUM_2] | (1 << school);
                    s[GameTag.TAG_SCRIPT_DATA_NUM_2] = mask;
                    s[GameTag.TAG_SCRIPT_DATA_NUM_1] = BEHighlightConditions.PlayedTwoSpellSchools((IPlayable)s) ? 2 : 1;
                })
            },
            PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!BEHighlightConditions.PlayedTwoSpellSchools((IPlayable)s)) return;
                new SummonCopyTask(EntityType.SOURCE, side: s.Zone == c.BoardZone ? SummonSide.RIGHT : SummonSide.DEFAULT)
                    .Process(g, c, s, null, stack);
            }) };
        }
    }
}
