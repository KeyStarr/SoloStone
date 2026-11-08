using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StonetalonStriker
    {
        internal const int DbfId = 122867;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION) {
                TriggerActivation = TriggerActivation.HAND,
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(p => p.Card.IsRace(Race.DRAGON)),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (s is Minion minion && s.Zone == c.HandZone)
                    {
                        using var handBuff = g.History ? g.PowerHistory.HandBuff(minion) : null;
                        int attack = minion.AttackDamage - (minion.AuraEffects?.ATK ?? 0) + 3;
                        int health = minion.BaseHealth - (minion.AuraEffects?.Health ?? 0) + 3;
                        Generic.ChangeEntityBlock(c, minion, Cards.FromId("CATA_551t"), false);
                        minion.AttackDamage = attack;
                        minion.BaseHealth = health;
                    }
                })
            } };
        }

    }
}
