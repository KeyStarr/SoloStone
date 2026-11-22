using System.Linq;
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
        // The boss minion's entity id, on the game entity (CoopRules.Apply finds it on the starting board).
        internal const GameTag BossEntity = (GameTag)34717;
        private static readonly System.Collections.Generic.HashSet<int> BossCards = new System.Collections.Generic.HashSet<int>();

        // A card that is a co-op boss: its death wins the game for the team.
        internal static void MarkBoss(Card card) => BossCards.Add(card.AssetId);

        // The boss and what the boss puts on a player's side (Explosive Rune) are the only characters a co-op player may attack:
        // the other side belongs to the teammate (playtest 2026-11-22: an Immune Nefarian's Taunt stopped applying, so the
        // teammate's hero and minions became attackable).
        private static readonly System.Collections.Generic.HashSet<int> BossSideCards = new System.Collections.Generic.HashSet<int>();
        internal static void MarkBossSide(Card card) => BossSideCards.Add(card.AssetId);
        public static bool IsBossSide(ICharacter character) =>
            character != null && (BossCards.Contains(character.Card.AssetId) || BossSideCards.Contains(character.Card.AssetId));

        internal static System.Collections.Generic.IEnumerable<ICharacter> AttackTargets(Game game, System.Collections.Generic.List<ICharacter> targets) =>
            IsCoop(game) ? targets.Where(IsBossSide).ToList() : (System.Collections.Generic.IEnumerable<ICharacter>)targets;
        // 34712..34719: the boss's own state (NefarianRises: phase, turns in phase, spell schedule, Bamboozles cast).
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 34710 && (int)tag <= 34719;

        public static bool IsCoop(Game game) => game != null && game[Coop] == 1;

        public static bool IsBossDefeated(Game game) => game != null && game[BossDefeated] == 1;

        internal static void Apply(Game game, bool coop)
        {
            if (!coop) return;
            game[Coop] = 1;
            foreach (var minion in game.Player1.BoardZone.GetAll().Concat(game.Player2.BoardZone.GetAll()))
                if (BossCards.Contains(minion.Card.AssetId)) { game[BossEntity] = minion.Id; break; }
        }

        // After each death phase (Game.CheckDeadHeroes and the death processing's own hero check): the boss has died.
        internal static void CheckBoss(Game game)
        {
            if (!IsCoop(game) || game[BossEntity] == 0 || IsBossDefeated(game) || game.State == State.COMPLETE) return;
            if (!game.IdEntityDic.TryGetValue(game[BossEntity], out var boss)) return;
            if (boss.Zone?.Type == Zone.GRAVEYARD || boss is Minion minion && minion.IsDead && minion.Zone?.Type != Zone.PLAY) DefeatBoss(game);
        }

        // The boss died (CheckBoss): the game ends after this action, and FinalWrapUp gives both players the win unless a
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
