using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TimelordNozdormu
    {
        internal const int DbfId = 120981;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Dormant = new DormantDefinition(5, "TIME_063e1", "TIME_063e2");
        }

        internal static void ConfigureListener(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_PLAY_CARD) {
                TriggerSource = TriggerSource.FRIENDLY,
                DormantHelperAfterCounteredPlay = true,
                // Cosmetic Coins are not expansion plays; the match retains its pinned meta.
                Condition = new SelfCondition(e => (int)e.Card.Set == e.Game.OfflinePools.NewestSetId && e.Card[(GameTag)2088] == 0),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (s is Enchantment helper && helper.Target is Minion m && m.Controller == c &&
                        m.Zone == c.BoardZone && m.IsDormant && !m.IsSilenced && t?.Id != m.Id &&
                        m.AppliedEnchantments.Contains(helper) && helper[GameTag.ATTACHED] == m.Id)
                        DormantRules.Accelerate(m, 1);
                })
            } };
        }
    }
}
