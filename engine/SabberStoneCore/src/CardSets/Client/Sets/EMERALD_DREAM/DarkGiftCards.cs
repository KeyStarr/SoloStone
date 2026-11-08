using System;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DarkGiftCards
    {
        internal static void Register(Action<int, string, Action<Card>> register)
        {
            register(115323, "EDR_102t", Token);
            register(113796, "EDR_100t", Token);
            register(113797, "EDR_100t1", Token);
            register(113798, "EDR_100t2", Token);
            register(113799, "EDR_100t3", Token);
            register(113801, "EDR_100t5", Token);
            register(113802, "EDR_100t6", Token);
            register(113882, "EDR_100t7", Token);
            register(113884, "EDR_100t8", Token);
            register(113886, "EDR_100t9", Token);
            register(120225, "EDR_100t13", Token);
            register(113803, "EDR_100te", card => Configure(card, Effects.Attack_N(3), Effects.Lifesteal));
            register(113804, "EDR_100t1e", card => Configure(card, Effects.Attack_N(2), Effects.Health_N(2), Effects.CantBeTargetedBySpellsAndHeroPowers));
            register(113805, "EDR_100t2e", card => Configure(card, Effects.ReduceCost(2), Effects.Attack_N(-2)));
            register(113806, "EDR_100t3e", card => Configure(card, Effects.Health_N(4), Effects.TauntEff));
            register(113808, "EDR_100t5e", card => {
                Configure(card);
                card.Power.Trigger = new Trigger(TriggerType.PLAY_MINION) {
                    TriggerSource = TriggerSource.ENCHANTMENT_TARGET,
                    SingleTask = new CustomTask((g, c, s, t, stack) => DarkGifts.LivingNightmare((Enchantment)s))
                };
            });
            register(113809, "EDR_100t6e", card => Configure(card, Effects.Charge));
            register(113883, "EDR_100t7e", card => Configure(card));
            register(115355, "EDR_100t8e1", card => Configure(card, Effects.Attack_N(4), Effects.Health_N(5)));
            register(113887, "EDR_100t9e", card => Configure(card, new Effect(GameTag.REBORN, EffectOperator.SET, 1)));
            register(120226, "EDR_100t13e", card => Configure(card, new Effect(GameTag.DIVINE_SHIELD, EffectOperator.SET, 1), Effects.Windfury));
            register(120020, "EDR_100t5e5", card => Configure(card, Effects.SetAttack(2), Effects.SetMaxHealth(2)));
        }

        private static void Token(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
        }

        private static void Configure(Card card, params IEffect[] effects)
        {
            Token(card);
            card.Power = new Power { Enchant = new Enchant(effects) };
        }
    }
}
