using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LohTheLivingLegend
    {
        internal const int DbfId = 118575;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("TLC_257e1", EntityType.CONTROLLER) };
        }

        internal static void ConfigureLegend(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Aura = new Aura(AuraType.HAND, Effects.SetCost(5)) { Condition = SelfCondition.IsMinion } };
        }
    }
}
