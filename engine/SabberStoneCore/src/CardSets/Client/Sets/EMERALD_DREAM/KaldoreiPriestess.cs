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
    internal static class KaldoreiPriestess
    {
        internal const int DbfId = 115461;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var enemy in c.Opponent.BoardZone.GetAll())
                    Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_970e"), (IPlayable)s, enemy);
                new EffectContext(g, c.Id, s.Id, stack: stack).Imbue();
            }) };
        }

        internal static void ConfigureDebuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power {
                Enchant = new Enchant(Effects.Attack_N(-2)),
                // Enchantment controller is the caster, even while attached to an enemy.
                Trigger = new Trigger(TriggerType.TURN_START) {
                    SingleTask = RemoveEnchantmentTask.Task,
                    RemoveAfterTriggered = true
                }
            };
        }
    }
}
