using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Mimicry
    {
        internal const int DbfId = 114508;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Rule: copy each actual draw result, including burns (official 32.0 notes); fatigue supplies no card.
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var draws = new[] { Generic.Draw(c.Opponent), Generic.Draw(c.Opponent) };
                foreach (var drawn in draws)
                    if (drawn != null) Generic.Copy(c, s, drawn, Zone.HAND);
            }) };
        }
    }
}
