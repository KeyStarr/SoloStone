using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    // Co-op boss games (the Nefarian Rises! Tavern Brawl): the two players are a team against a boss minion. Both win together
    // when the boss dies, and both lose together when either hero dies or either player concedes ("Nefarian wins if he defeats
    // either of you!"). GameConfig.Coop marks the game at setup; the mark lives on the game entity, so a snapshot or clone
    // keeps it, and every other game is untouched.
    public static class CoopRules
    {
        // Private, never sent to the client (NativeMatch and the transport invariants filter the range).
        internal const GameTag Coop = (GameTag)34710;
        internal const GameTag BossDefeated = (GameTag)34711;
        // 34712..34719: the boss's own state (NefarianRises: phase, turns in phase, spell schedule, Bamboozles cast).
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 34710 && (int)tag <= 34719;

        public static bool IsCoop(Game game) => game != null && game[Coop] == 1;

        public static bool IsBossDefeated(Game game) => game != null && game[BossDefeated] == 1;

        internal static void Apply(Game game, bool coop)
        {
            if (coop) game[Coop] = 1;
        }

        // The boss died (its Deathrattle): the game ends after this action, and FinalWrapUp gives both players the win unless a
        // hero fell or a player conceded too.
        internal static void DefeatBoss(Game game)
        {
            if (!IsCoop(game) || game.State == State.COMPLETE) return;
            game[BossDefeated] = 1;
            game.NextStep = Step.FINAL_WRAPUP;
        }

        // FinalWrapUp for a co-op game: one shared result.
        internal static void WrapUp(Game game)
        {
            bool lost = false;
            foreach (Controller player in new[] { game.Player1, game.Player2 })
                lost |= player.PlayState == PlayState.LOSING || player.PlayState == PlayState.CONCEDED || player.PlayState == PlayState.TIED
                    || player.PlayState == PlayState.LOST;
            // A boss dying in the same cleanup as a hero still means a hero was defeated: the team loses.
            PlayState shared = lost || !IsBossDefeated(game) ? PlayState.LOST : PlayState.WON;
            game.Player1.PlayState = shared;
            game.Player2.PlayState = shared;
        }
    }
}
