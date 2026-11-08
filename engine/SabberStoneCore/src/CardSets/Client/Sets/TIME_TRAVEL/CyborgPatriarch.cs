using SabberStoneCore.Model;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CyborgPatriarch
    {
        internal const int DbfId = 120857;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Dormant = new DormantDefinition(3, "TIME_046e");
        }
    }
}
