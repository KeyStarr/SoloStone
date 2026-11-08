using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DoomsdayPrepper
    {
        [StandardCard("TIME_021", 120179)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (OutcastRules.PlayedFromEdge((IPlayable)s))
                    Generic.AddEnchantmentBlock(g, Cards.FromId("BE_784e"), (IPlayable)s, c.Hero);
            }) };
        }
        // TIME_021 has no exported helper. Titan Bane has exactly the required text and lifetime.
        [StandardCard("BE_784e", 132912)]
        internal static void ConfigureImmunity(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                Aura = new Aura(AuraType.ATTACHED, new Effect(GameTag.IMMUNE, EffectOperator.ADD, 1)),
                Trigger = new Trigger(TriggerType.TURN_START) {
                    TriggerSource = TriggerSource.FRIENDLY, RemoveAfterTriggered = true,
                    SingleTask = RemoveEnchantmentTask.Task
                }
            };
        }
    }
}
