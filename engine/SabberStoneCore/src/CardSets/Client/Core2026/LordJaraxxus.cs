using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Core2026
{
    internal static class LordJaraxxus
    {
        internal const int DbfId = 69637;
        internal static void Configure(Card card)
        {
            // Hero-card replacement, health/armor retention and Hero Power come from native PlayHero.
            card.Power = new Power { PowerTask = new WeaponTask("EX1_323w") };
        }
    }
}
