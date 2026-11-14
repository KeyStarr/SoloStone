using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// Player report where a match stopped progressing (issue #80): after the card resolves, the player whose turn it is
// must have a legal End Turn and the turn must pass.
internal static class MatchSoftlockProof
{
    internal static void Run()
    {
        StadiumAnnouncerRewind();
        Console.WriteLine("PASS match softlock regressions (#80)");
    }

    private static Game New(CardClass hero)
    {
        var game = new Game(new GameConfig { StartPlayer = 1, RandomSeed = 43, Shuffle = false, SkipMulligan = true, History = true,
            Logging = false, Player1HeroClass = hero, Player2HeroClass = hero,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        return game;
    }

    private static void TurnPasses(Game game, string label)
    {
        var player = game.CurrentPlayer;
        Core2026Proof.Check(player.Choice == null && !game.GetPendingDecisions().Any(d => d.Kind is not (DecisionKind.Options or DecisionKind.Target or DecisionKind.ChooseOne)), label + ": no decision left open: "
            + player.Choice?.ChoiceType + "/" + player.Choice?.ChoiceAction + " " + string.Join(",", game.GetPendingDecisions().Select(d => d.ControllerId + ":" + d.Kind)));
        var end = player.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.END_TURN);
        Core2026Proof.Check(end != null, label + ": End Turn offered");
        Core2026Proof.Check(game.Process(end) && game.CurrentPlayer != player, label + ": turn passes");
        var next = game.CurrentPlayer.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.END_TURN);
        Core2026Proof.Check(next != null && game.Process(next) && game.CurrentPlayer == player, label + ": next turn passes back");
    }

    // #80: a Death Knight's Herald Soldier (Soldier of Onyxia, Onyxia's Wing) hands out a random minion that costs Health this
    // turn. Stadium Announcer bought that way, then rewound, froze the board.
    private static void StadiumAnnouncerRewind()
    {
        // mana; Onyxia's Wing (Health through an enchantment actor); Soldier of Onyxia (Health, no actor).
        foreach (string payment in new[] { "mana", "wing", "soldier" })
        foreach (bool second in new[] { false, true })
        {
            bool health = payment != "mana";
            string label = $"Stadium Announcer Rewind ({payment}, {(second ? "second" : "first")} player)";
            var game = New(CardClass.DEATHKNIGHT);
            if (second) End(game);
            var player = game.CurrentPlayer;
            player.BaseMana = 10; player.UsedMana = 0;
            var announcer = (Playable)Add(player, "TIME_034");
            if (payment == "wing")
            {
                var actor = Enchantment.GetInstance(player, player.Hero, announcer, Cards.FromId("CATA_155e2"));
                CostRules.AddPaymentOverride(announcer, CostPaymentKind.Health, game.Turn, actor.Id);
            }
            else if (payment == "soldier") CostRules.AddPaymentOverride(announcer, CostPaymentKind.Health, game.Turn);
            int hero = player.Hero.Health, cost = announcer.Cost;
            var play = player.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == announcer.Id);
            Core2026Proof.Check(play != null && game.Process(play), label + ": played");
            Core2026Proof.Check(RewindTestHelpers.IsRewindOffered(game), label + ": Rewind offered");
            RewindTestHelpers.AnswerRewind(game);
            Core2026Proof.Check(player.Hero.Weapon != null && player.Opponent.Hero.Weapon != null, label + ": replay equips both weapons");
            Core2026Proof.Check(announcer.Zone == player.BoardZone, label + ": Announcer stays on the board");
            Core2026Proof.Check(health ? player.Hero.Health == hero - cost && player.UsedMana == 0 : player.UsedMana == cost && player.Hero.Health == hero,
                label + ": paid once");
            TurnPasses(game, label);
        }
    }
}
