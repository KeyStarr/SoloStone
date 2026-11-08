using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TwilightEgg
    {
        internal const int DbfId = 123126;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                Trigger = new Trigger(TriggerType.TURN_START) {
                    SingleTask = new CustomTask((g, c, s, t, stack) => {
                        if (s.Zone != c.BoardZone) return;
                        s[GameTag.TAG_SCRIPT_DATA_NUM_1]++;
                        s[GameTag.TAG_SCRIPT_DATA_NUM_2]++;
                    })
                },
                DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                    if (c.BoardZone.IsFull) return;
                    var whelp = (Minion)Entity.FromCard(c, Cards.FromId("CATA_210t"),
                        new EntityData { { GameTag.DISPLAYED_CREATOR, s.Id } });
                    whelp.AttackDamage = s[GameTag.TAG_SCRIPT_DATA_NUM_1];
                    whelp.BaseHealth = s[GameTag.TAG_SCRIPT_DATA_NUM_2];
                    int position = EffectContext.DeathrattlePosition(c, s);
                    Generic.SummonBlock(g, whelp, position, s);
                })
            };
        }
    }
}
