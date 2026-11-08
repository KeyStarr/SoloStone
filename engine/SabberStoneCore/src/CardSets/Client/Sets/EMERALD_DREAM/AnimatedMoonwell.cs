using System;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AnimatedMoonwell
    {
        internal const int DbfId = 114533;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_CAST) {
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (!(s is Minion minion) || minion.Zone != c.BoardZone || minion.IsSilenced || !(t is Spell)) return;
                    // Payment records zero and Health-paid modified Cost before discounts are consumed.
                    int cost = Math.Max(0, t[GameTag.TAG_LAST_KNOWN_COST_IN_HAND]);
                    if (cost > 0) new EffectContext(g, c.Id, s.Id, stack: stack).GiveBuff(s.Id, "EDR_254e1", cost);
                })
            } };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0)) { UseScriptTag = true } };
        }
    }
}
