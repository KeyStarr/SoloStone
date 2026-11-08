using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Contingency
    {
        internal const int DbfId = 120208;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                for (int i = 0; i < 2; i++)
                    if (c.DeckZone.IsEmpty) Generic.Draw(c);
                    else Generic.Draw(c, 0); // DeckZone.Draw normally removes Count - 1 (the top).
            }) };
        }
    }
}
