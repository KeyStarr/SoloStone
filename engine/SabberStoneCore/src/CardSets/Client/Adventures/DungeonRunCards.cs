using System;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Adventures
{
    // Dungeon Run (Kobolds & Catacombs adventure, LOOTA_*) cards, one module per round band under DungeonRun/. None of them
    // are collectible, and all stay out of the legacy random pools, so no random effect or recorded game draws them.
    internal static class DungeonRunCards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            DungeonRun.DungeonRunRound1.Apply(register);
            DungeonRun.DungeonRunRounds2To3.Apply(register);
            DungeonRun.DungeonRunRounds4To5.Apply(register);
            DungeonRun.DungeonRunRounds6To7.Apply(register);
            DungeonRun.DungeonRunFinals.Apply(register);
        }

        internal static void Plain(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
        }
    }
}
