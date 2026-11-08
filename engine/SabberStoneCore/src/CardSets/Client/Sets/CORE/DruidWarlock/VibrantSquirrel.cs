using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class VibrantSquirrel
    {
        internal const int DbfId = 120393;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new AddCardTo("SW_439t", EntityType.DECK, 4) };
        }

        internal static void ConfigureAcorn(Card card)
        {
            card.Implemented = true;
            card.Power = new Power {
                PowerTask = new SummonTask("SW_439t2"),
                TopdeckTask = new CustomTask((g, c, s, t, stack) => {
                    new SummonTask("SW_439t2").Process(g, c, s, null, stack);
                    // A full board stops the summon task, but must not stop the replacement draw.
                    new DrawTask().Process(g, c, s, null, stack);
                })
            };
        }
    }
}
