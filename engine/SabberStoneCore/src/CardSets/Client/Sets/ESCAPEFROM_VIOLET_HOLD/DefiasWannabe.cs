using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DefiasWannabe
    {
        internal const int DbfId = 124075;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                ComboTask = new CustomTask((g, c, s, t, stack) => {
                    Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_909e"), (IPlayable)s, (IPlayable)s, c.NumCardsPlayedThisTurn - 1);
                }),
                Trigger = new ComboDisplay(
                    Display(TriggerType.AFTER_PLAY_CARD), Display(TriggerType.TURN_START))
            };
        }

        private static Trigger Display(TriggerType type) => new Trigger(type) {
            TriggerActivation = TriggerActivation.HAND,
            TriggerSource = TriggerSource.FRIENDLY,
            SingleTask = new CustomTask((g, c, s, t, stack) => s[GameTag.TAG_SCRIPT_DATA_NUM_1] = c.NumCardsPlayedThisTurn)
        };

        private sealed class ComboDisplay : MultiTrigger
        {
            internal ComboDisplay(params Trigger[] triggers) : base(triggers) { }
            public override Trigger Activate(IPlayable source, TriggerActivation activation = TriggerActivation.PLAY,
                bool cloning = false, bool asAncillary = false)
            {
                if (activation == TriggerActivation.HAND && !cloning)
                    source[GameTag.TAG_SCRIPT_DATA_NUM_1] = source.Controller.NumCardsPlayedThisTurn;
                return base.Activate(source, activation, cloning, asAncillary);
            }
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0), Effects.Health_N(0)) { UseScriptTag = true } };
        }
    }
}
