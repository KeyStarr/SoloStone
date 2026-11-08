using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BlowtorchSaboteur
    {
        internal const int DbfId = 120433;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var tax = Cards.FromId("DRG_403e");
                // Opponent owns the aura so replacement Hero Powers inherit it (Entity.FromCard).
                var enchantment = Enchantment.GetInstance(c.Opponent, (IPlayable)s, c.Opponent, tax);
                tax.Power.Aura.Activate(enchantment);
                tax.Power.Trigger.Activate(enchantment);
            }) };
        }

        internal static void ConfigureTax(Card card)
        {
            // The legacy XML has the old set-to-three tooltip; retain the pinned current enchantment text.
            card.Text = "Costs (2) more.";
            card.Implemented = true;
            card.Power = new Power {
                Aura = new Aura(AuraType.HEROPOWER, Effects.AddCost(2)),
                Trigger = new Trigger(TriggerType.INSPIRE) {
                    TriggerSource = TriggerSource.FRIENDLY,
                    RemoveAfterTriggered = true,
                    SingleTask = new CustomTask((g, c, s, t, stack) => ((Enchantment)s).OngoingEffect.Remove())
                }
            };
        }
    }
}
