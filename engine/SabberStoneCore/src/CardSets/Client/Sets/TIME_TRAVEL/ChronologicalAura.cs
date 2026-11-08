using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ChronologicalAura
    {
        [StandardCard("TIME_700", 120644)]
        internal static void Configure(Card card) => AuraSpellRules.Declare(card, effect: new SummonTask("TIME_700t"));
        [StandardCard("TIME_700t", 122736)]
        internal static void ConfigureDrake(Card card) => card.Implemented = true;
    }
}
