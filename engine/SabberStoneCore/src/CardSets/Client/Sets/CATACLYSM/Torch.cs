using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Torch
    {
        internal const int DbfId = 123164;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_DAMAGED_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion minion)) return;
                int health = minion.Health;
                int damage = Generic.DamageCharFunc((IPlayable)s, minion, s[GameTag.TAG_SCRIPT_DATA_NUM_1], true);
                int excess = damage - health;
                if (excess > 0)
                    Generic.Copy(c, s, (IPlayable)s, Zone.HAND, initialize: copy => copy[GameTag.TAG_SCRIPT_DATA_NUM_1] = excess);
            }) };
        }
    }
}
