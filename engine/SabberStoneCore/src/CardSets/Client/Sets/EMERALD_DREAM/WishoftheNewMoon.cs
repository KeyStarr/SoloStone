using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WishoftheNewMoon
    {
        internal const int DbfId = 114299;
        internal static void Configure(Card card)
        {
            ConfigureFullMoon(card);
            card.ExcludeFromLegacyRandomPools = true;
            card.Power.Trigger = new Trigger(TriggerType.CAST_SPELL) {
                TriggerActivation = TriggerActivation.HAND,
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (s.Zone != c.HandZone) return;
                    if (--s[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0)
                        new ChangeEntityTask("EDR_460t").Process(g, c, s, null, stack);
                })
            };
        }

        internal static void ConfigureFullMoon(Card card)
        {
            card.Implemented = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = new DamageTask(6, EntityType.TARGET, spellDmg: true) };
        }
    }
}
