using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BlessingOfTheInfinite
    {
        internal static void Configure(Card card) => card.Implemented = true;
        internal static void ConfigureAttack(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0)) { UseScriptTag = true } };
        }
    }
}
