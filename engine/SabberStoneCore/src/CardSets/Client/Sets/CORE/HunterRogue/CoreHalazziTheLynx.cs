using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreHalazziTheLynx
    {
        internal const int DbfId = 126891;
        internal static void Configure(Card card)
        {
            // Retain the already configured legacy/rebalanced power and legacy pool membership.
            card.Implemented = true;
            Cards.FromId("TRL_348t").Tags[(GameTag)1584] = 1;
        }
    }
}
