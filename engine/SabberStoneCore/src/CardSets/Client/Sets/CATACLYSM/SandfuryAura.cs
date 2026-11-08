using SabberStoneCore.Actions;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SandfuryAura
    {
        [StandardCard("CATA_480", 123691)]
        internal static void Configure(Card card) => AuraSpellRules.Declare(card);
    }
}
