using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MusterforBattle
    {
        internal const int DbfId = 96916;
        internal static void Configure(Card card)
        {
            // Retain inherited legacy/rebalanced power and existing legacy pool membership.
            card.Implemented = true;
            card.Text = "Summon three {0} Silver Hand Recruits. Equip a 1/4 Weapon.";
            card.Tags[(GameTag)858] = 2029;
        }
    }
}
