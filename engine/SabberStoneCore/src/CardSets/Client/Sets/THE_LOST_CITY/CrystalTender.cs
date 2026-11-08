using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CrystalTender
    {
        internal const int DbfId = 118259;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int difference = c.Opponent.BaseMana - c.BaseMana;
                if (difference > 0) Generic.ChangeManaCrystal(c, difference, false);
            }) };
        }
    }
}
