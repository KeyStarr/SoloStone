using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

// Run-mode game setup (GameConfig, Dungeon Run first): starting Health, mana before the first turn, no Coin for the second
// player, and loadout cards that resolve with Start of Game. All of it is setup-only: snapshots keep the result, not the knobs.
internal static class RunSetupProof
{
    private static List<Card> Deck(params string[] ids) => ids.Select(Cards.FromId).ToList();

    private static Game Start(Action<GameConfig> setup, string[] player1Deck = null, string[] player2Deck = null)
    {
        var config = new GameConfig {
            StartPlayer = 1, Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.WARRIOR,
            Player1Deck = Deck(player1Deck ?? Enumerable.Repeat("CS2_182", 10).ToArray()),
            Player2Deck = Deck(player2Deck ?? Enumerable.Repeat("CS2_120", 10).ToArray()),
            Shuffle = false, SkipMulligan = true, History = true, RandomSeed = 7,
        };
        setup(config);
        var game = new Game(config);
        game.StartGame();
        return game;
    }

    [GroupProof("run-setup")]
    internal static void Run()
    {
        // The ordinary game is unchanged: card Health, a Coin for the second player, no early mana.
        Game plain = Start(_ => { });
        Core2026Proof.Check(plain.Player1.Hero.Health == 30 && plain.Player2.Hero.Health == 30, "default: hero card Health");
        Core2026Proof.Check(plain.Player2.HandZone.Count == 5 && plain.Player2.HandZone.Any(c => c.Card.Id == "GAME_005"), "default: Coin and four cards");
        Core2026Proof.Check(plain.Player2.BaseMana == 0, "default: no mana before the first turn");

        // Dungeon Run round 1 against a round-8 style boss: 15 Health, boss with no Coin and 2 crystals.
        Game run = Start(c => { c.Player1StartingHealth = 15; c.Player2StartingHealth = 50; c.SkipCoin = true; c.Player2StartingMana = 2; });
        Core2026Proof.Check(run.Player1.Hero.Health == 15 && run.Player1.Hero.BaseHealth == 15, "player starts at 15 Health (max 15)");
        Core2026Proof.Check(run.Player2.Hero.Health == 50 && run.Player2.Hero.BaseHealth == 50, "boss starts at 50 Health");
        Core2026Proof.Check(run.Player2.HandZone.Count == 4 && run.Player2.HandZone.All(c => c.Card.Id != "GAME_005"),
            "the second player keeps its fourth card but gets no Coin");
        Core2026Proof.Check(run.Process(EndTurnTask.Any(run.Player1)), "end the player's first turn");
        Core2026Proof.Check(run.Player2.BaseMana == 3 && run.Player2.RemainingMana == 3, "two starting crystals: 3 mana on the boss's first turn");
        Core2026Proof.Check(run.Player1.BaseMana == 1, "the player's own mana is unchanged");

        // Setup-only: a snapshot keeps the result, and the restored game still plays.
        Game loaded = Game.LoadSnapshot(run.SaveSnapshot());
        Core2026Proof.Check(HsSim.Canonical.State(loaded) == HsSim.Canonical.State(run), "a run game survives its snapshot");
        Core2026Proof.Check(loaded.Player1.Hero.Health == 15 && loaded.Player2.HandZone.All(c => c.Card.Id != "GAME_005"),
            "the restored game keeps 15 Health and no Coin");
        Core2026Proof.Check(loaded.Process(EndTurnTask.Any(loaded.Player2)) && loaded.Player1.BaseMana == 2, "the restored game plays on");

        // Loadout: set aside before the deal, resolved with Start of Game after the mulligans; eligibility still applies.
        // Nozdormu stands in for a passive treasure: its Start of Game needs one in the opponent's deck too.
        Game eligible = Start(c => c.Player1Loadout = new[] { "CS3_035" }, player2Deck: new[] { "CS3_035", "CS2_120", "CS2_120", "CS2_120", "CS2_120" });
        var source = eligible.Player1.SetasideZone.Single(e => e.Card.Id == "CS3_035");
        Core2026Proof.Check(eligible.Player1[NativeTailRules.NozdormuEnabled] == 1 && eligible.Player2[NativeTailRules.NozdormuEnabled] == 1
            && eligible.Player1.TimeOut == 15 && eligible.Player2.TimeOut == 15, "a loadout card's Start of Game resolves");
        Core2026Proof.Check(source.Zone.Type == Zone.SETASIDE && eligible.Player1.DeckZone.All(c => c.Card.Id != "CS3_035")
            && eligible.Player1.HandZone.All(c => c.Card.Id != "CS3_035"), "the loadout card stays set aside, never dealt");
        Core2026Proof.Check(eligible.Process(EndTurnTask.Any(eligible.Player1)) && eligible.Process(EndTurnTask.Any(eligible.Player2))
            && eligible.Player1.TimeOut == 15 && source.Zone.Type == Zone.SETASIDE, "the loadout effect holds over later turns");
        Game ineligible = Start(c => c.Player1Loadout = new[] { "CS3_035" });
        Core2026Proof.Check(ineligible.Player1[NativeTailRules.NozdormuEnabled] == 0 && ineligible.Player1.TimeOut == 75,
            "an ineligible loadout card does nothing");

        // The opponent AI's fair view keeps a loadout card (both players are shown it; masked, its aura lost its owner and
        // the search copy failed), but not another set-aside Start of Game card such as an unchosen Discover option.
        Game shown = Start(c => c.Player1Loadout = new[] { "CS3_035" }, player2Deck: new[] { "CS3_035", "CS2_120", "CS2_120", "CS2_120", "CS2_120" });
        var loadout = shown.Player1.SetasideZone.Single(e => e.Card.Id == "CS3_035");
        var skipped = SabberStoneCore.Model.Entities.Entity.FromCard(shown.Player1, Cards.FromId("CS3_035"),
            new SabberStoneCore.Model.Entities.EntityData { [GameTag.ZONE] = (int)Zone.SETASIDE }, shown.Player1.SetasideZone);
        Core2026Proof.Check(shown.Process(EndTurnTask.Any(shown.Player1)), "end the player's turn before the AI's view");
        var view = FairBotState.Create(shown, shown.Player2.PlayerId, 7, DeckStyle.Value).FingerprintGame;
        Core2026Proof.Check(view.IdEntityDic.TryGetValue(loadout.Id, out var seen) && !(seen is SabberStoneCore.Model.Entities.Unknown)
            && seen.Card.Id == "CS3_035", "fair view: the loadout card is visible");
        Core2026Proof.Check(!view.IdEntityDic.TryGetValue(skipped.Id, out var hidden) || hidden is SabberStoneCore.Model.Entities.Unknown
            || hidden.Card.Id != "CS3_035", "fair view: an unchosen Start of Game card stays hidden");
        Core2026Proof.Check(view.CloneExact() != null, "fair view: the view copies");

        bool refused = false;
        try { Start(c => c.Player2Loadout = new[] { "CS2_182" }); }
        catch (InvalidOperationException) { refused = true; }
        Core2026Proof.Check(refused, "a loadout card without a Start of Game effect is refused");
        foreach (Action<GameConfig> bad in new Action<GameConfig>[] { c => c.Player1StartingHealth = -1, c => c.Player2StartingMana = 11 })
        {
            bool rejected = false;
            try { Start(bad); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Core2026Proof.Check(rejected, "out-of-range starting Health or mana is refused");
        }
    }
}
