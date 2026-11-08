using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AshWorm
    {
        [StandardCard("MEND_040", 123472)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Dormant = new DormantDefinition(null, "MEND_040e2", awakenCondition: SelfCondition.IsBoardFull);
            card.Power = new Power();
        }
        [StandardCard("MEND_040e2", 123663)]
        internal static void ConfigureCondition(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}
