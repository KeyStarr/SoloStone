using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EbonscaleScout
    {
        internal const int DbfId = 122869;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            ConfigureBattlecry(card);
            card.Power.Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION) {
                TriggerActivation = TriggerActivation.HAND,
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(p => p.Card.IsRace(Race.DRAGON)),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (s is Minion minion && s.Zone == c.HandZone)
                    {
                        using var handBuff = g.History ? g.PowerHistory.HandBuff(minion) : null;
                        int attack = minion.AttackDamage - (minion.AuraEffects?.ATK ?? 0) + 4;
                        int health = minion.BaseHealth - (minion.AuraEffects?.Health ?? 0) + 4;
                        Generic.ChangeEntityBlock(c, minion, Cards.FromId("CATA_552t"), false);
                        minion.AttackDamage = attack;
                        minion.BaseHealth = health;
                    }
                })
            };
        }

        internal static void ConfigureBattlecry(Card card)
        {
            card.Implemented = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (s is Minion minion && t is ICharacter)
                    new EffectContext(g, c.Id, s.Id, t.Id, stack).Damage(t.Id, minion.AttackDamage);
            }) };
        }
    }
}
