using System;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Adventures
{
    // Dungeon Run (Kobolds & Catacombs adventure, LOOTA_*) cards, one module per round band under DungeonRun/. None of them
    // are collectible, and all stay out of the legacy random pools, so no random effect or recorded game draws them.
    public static class DungeonRunCards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            DungeonRun.DungeonRunRound1.Apply(register);
            DungeonRun.DungeonRunRounds2To3.Apply(register);
            DungeonRun.DungeonRunRounds4To5.Apply(register);
            DungeonRun.DungeonRunRounds6To7.Apply(register);
            DungeonRun.DungeonRunFinals.Apply(register);
            DungeonRun.DungeonRunTreasures.Apply(register);
            DungeonRun.DungeonRunActiveTreasures.Apply(register);
        }

        // The active treasures, as data/runs/loot-v1.json lists them (treasures.active): what a Treasure Coffer can add to the
        // player's Dungeon Deck. The runs suite checks that the two lists agree.
        public static readonly System.Collections.Generic.IReadOnlyList<string> ActiveTreasures = new[] {
            "LOOTA_819", "LOOTA_836", "LOOTA_839", "LOOTA_823", "LOOTA_842a", "LOOTA_812", "LOOTA_834", "LOOTA_842b",
            "LOOTA_829", "LOOTA_813", "LOOTA_847", "LOOTA_816", "LOOTA_841", "LOOTA_826", "LOOTA_843", "LOOTA_820",
            "LOOTA_805", "LOOTA_835", "LOOTA_837", "LOOTA_821", "LOOTA_827", "LOOT_998k", "LOOTA_811", "LOOTA_806",
            "LOOTA_840", "LOOT_998l", "LOOTA_838", "LOOTA_817", "LOOTA_830", "LOOTA_822", "LOOTA_814"
        };

        // A random index for a run card's own choice, announced like the engine's random tasks do.
        internal static int Pick(Game game, int count)
        {
            game.OnRandomHappened(true);
            return game.Random.Next(count);
        }

        internal static void Plain(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
        }
    }
}
