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
        GeneratedAnnouncerRewind();
        RewindCardSweep();
        Console.WriteLine("PASS match softlock regressions (#80)");
    }

    private static Game New(CardClass hero, int seed = 43)
    {
        var game = new Game(new GameConfig { StartPlayer = 1, RandomSeed = seed, Shuffle = false, SkipMulligan = true, History = true,
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

    // #80: the report's own route. Arisen Onyxia (Colossal) summons Wings; a Wing's AFTER_SUMMON hands out a random minion that
    // costs Health, tied to an actor enchantment the Wing owns. The Wing's gift (or a Soldier's, from Herald) is turned into
    // Stadium Announcer in place (same entity, payment override and actor), then played and rewound.
    private static void GeneratedAnnouncerRewind()
    {
        foreach (string route in new[] { "wing", "soldier" })
        foreach (bool second in new[] { false, true })
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var game = New(CardClass.DEATHKNIGHT, seed);
            if (second) End(game);
            var player = game.CurrentPlayer;
            player.BaseMana = 10; player.UsedMana = 0;
            int handBefore = player.HandZone.Count;
            if (route == "wing")
            {
                var onyxia = (Playable)Add(player, "CATA_155");
                var cast = player.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == onyxia.Id);
                Core2026Proof.Check(cast != null && game.Process(cast), "Arisen Onyxia played");
            }
            else
            {
                var herald = (Playable)Add(player, "CATA_785");
                for (int i = 0; i < 3; i++) HeraldRules.Resolve(player, herald);
                game.DeathProcessingAndAuraUpdate();
            }
            var gifts = player.HandZone.OfType<Playable>().Where(c => c.Card.Id != "CS2_182" && c.Card.Id != "CATA_785" && c.Card.Type == CardType.MINION
                && CostRules.PaymentKind(c) == CostPaymentKind.Health).ToList();
            Core2026Proof.Check(gifts.Count > 0, $"{route}: Herald gift in hand (seed {seed})");
            var gift = gifts[0];
            var announcer = (Playable)Generic.ChangeEntityBlock(player, gift, Cards.FromId("TIME_034"), false);
            string label = $"{route}-generated Stadium Announcer Rewind (seed {seed}, {(second ? "second" : "first")} player)";
            Core2026Proof.Check(CostRules.PaymentKind(announcer) == CostPaymentKind.Health && announcer.Zone == player.HandZone, label + ": gift kept its Health cost");
            int hero = player.Hero.Health;
            var play = player.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == announcer.Id);
            Core2026Proof.Check(play != null && game.Process(play), label + ": played");
            Core2026Proof.Check(RewindTestHelpers.IsRewindOffered(game), label + ": Rewind offered");
            RewindTestHelpers.AnswerRewind(game);
            Core2026Proof.Check(player.Hero.Weapon != null && player.Opponent.Hero.Weapon != null, label + ": replay equips both weapons");
            Core2026Proof.Check(announcer.Zone == player.BoardZone, label + ": Announcer stays on the board");
            TurnPasses(game, label);
        }
    }

    // Sweep for the Rewind class (#80): every implemented Rewind card, played from either seat, hand-created and as a Herald gift
    // (same entity, Health cost and actor), with the Rewind answered. Each case must leave the turn able to pass.
    private static void RewindCardSweep()
    {
        var rewindCards = Cards.All.Where(c => c.Implemented && c.Collectible && c[GameTag.REWIND] == 1 && c.Type != CardType.HERO_POWER
            && (c.Type == CardType.MINION || c.Type == CardType.SPELL || c.Type == CardType.WEAPON)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        int cases = 0, softlocks = 0, unplayable = 0;
        var failures = new List<string>();
        foreach (var card in rewindCards)
        foreach (bool second in new[] { false, true })
        foreach (bool gift in new[] { false, true })
        {
            if (gift && card.Type != CardType.MINION) continue;
            string label = $"{card.Id} ({(second ? "second" : "first")} seat, {(gift ? "Herald gift" : "hand")})";
            try
            {
                var game = New(CardClass.DEATHKNIGHT, 7);
                if (second) End(game);
                var player = game.CurrentPlayer;
                player.BaseMana = 10; player.UsedMana = 0;
                var subject = (Playable)Add(player, card.Id);
                if (gift) CostRules.AddPaymentOverride(subject, CostPaymentKind.Health, game.Turn);
                var play = player.Options().FirstOrDefault(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == subject.Id);
                if (play == null || !game.Process(play)) { unplayable++; continue; }
                cases++;
                for (int guard = 0; guard < 8 && (player.Choice != null || game.GetPendingDecisions().Any(d => d.Kind is not (DecisionKind.Options or DecisionKind.Target or DecisionKind.ChooseOne))); guard++)
                {
                    if (RewindTestHelpers.IsRewindOffered(game)) { RewindTestHelpers.AnswerRewind(game); continue; }
                    var pending = game.GetPendingDecisions().First(d => d.Kind is not (DecisionKind.Options or DecisionKind.Target or DecisionKind.ChooseOne));
                    var first = pending.Options.First();
                    Core2026Proof.Check(game.TryAnswer(new DecisionAnswer(pending.Id, pending.ControllerId, new[] { first.OptionId }), out var error), "answer: " + error);
                }
                TurnPasses(game, label);
            }
            catch (Exception error)
            {
                softlocks++; failures.Add(label + ": " + error.Message.Split('\n')[0]);
            }
        }
        Console.WriteLine($"Rewind sweep: {rewindCards.Count} cards, {cases} played cases, {unplayable} not playable bare (need a target/board), {softlocks} failed");
        foreach (string failure in failures) Console.WriteLine("  SWEEP FAIL " + failure);
        Core2026Proof.Check(softlocks == 0, "Rewind sweep: " + softlocks + " case(s) left the turn unable to pass: " + string.Join(" | ", failures.Take(5)));
    }
}
