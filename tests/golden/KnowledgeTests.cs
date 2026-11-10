using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Kettle;
using static ReplyTests;

public static class KnowledgeTests
{
    public static void TestPublicHandRebuild() => AuditReproTests.Audit20261031_C3_PublicHandKnowledgeSurvivesRebuild();
    public static void TestSecretProbeLethal() => AuditReproTests.Audit20261031_C2_SecretLethalSurvivesAuthority();

    private static Game SecretBoard(CardClass hero, params string[] secrets)
    {
        var g = Fixture(CardClass.MAGE, hero);
        Check(g.Process(EndTurnTask.Any(g.Player1)), "enemy turn");
        foreach (string id in secrets) {
            g.Player2.UsedMana = 0;
            var secret = Entity.FromCard(g.Player2, Cards.FromId(id), zone:
                Cards.FromId(id).Class == hero ? (SabberStoneCore.Model.Zones.IZone)g.Player2.DeckZone : g.Player2.HandZone);
            if (secret.Zone == g.Player2.DeckZone) SabberStoneCore.Actions.Generic.Draw(g.Player2, secret);
            Check(g.Process(PlayCardTask.Spell(g.Player2, secret)), "arm " + id);
        }
        Check(g.Process(EndTurnTask.Any(g.Player2)), "own turn");
        foreach (var card in g.Player1.HandZone.ToArray()) { g.Player1.HandZone.Remove(card); g.Player1.SetasideZone.Add(card); }
        g.Player2.Hero.Damage = 24;
        Entity.FromCard(g.Player1, Cards.FromId("CS2_029"), zone: g.Player1.HandZone);
        Entity.FromCard(g.Player1, Cards.FromId("GAME_005"), zone: g.Player1.HandZone);
        return g;
    }
    public static void TestHiddenSecretIdentityCannotChangeLethalOrDecision()
    {
        var original = SecretBoard(CardClass.MAGE, "CORE_EX1_287");
        var twin = original.CloneExact(); var old = twin.Player2.SecretZone.Single();
        old.ActivatedTrigger.Remove(); twin.Player2.SecretZone.Remove(old);
        var swapped = Entity.FromCard(twin.Player2, Cards.FromId("CORE_EX1_289"), zone: twin.Player2.SecretZone, id: old.Id);
        swapped.Card.Power.Trigger.Activate(swapped);
        string expected = null;
        foreach (var game in new[] { original, twin }) {
            var pilot = new GoldenPilot(NpcCatalog.All[0]); var view = pilot.Options.FairViewFactory(game, 1, 71, DeckStyle.Tempo);
            var fireball = view.Actions.Single(a => a.Type == PlayerTaskType.PLAY_CARD && a.Target == view.Facts.Opponent.Hero.EntityId);
            var verify = LethalJob.Verify(view, new[] { fireball }, 2000); Run(verify);
            Check(verify.Result.Outcome != LethalOutcome.Found, "Fireball alone must not be certified");
            var result = pilot.Decide(view, new GoldenContext(game.Turn, 0, 71, pilot.Profile));
            string trace = result.Action + ":" + result.Value + ":" + result.Nodes;
            if (expected == null) expected = trace; else Check(expected == trace, "actual hidden Secret changed decision");
        }
    }
    public static void TestSecretWorldsCoverClassPoolAndOrders()
    {
        foreach (var spec in new[] { (CardClass.MAGE, "CORE_EX1_287"), (CardClass.HUNTER, "EX1_610"), (CardClass.PALADIN, "EX1_130") }) {
            var view = View(SecretBoard(spec.Item1, spec.Item2));
            var worlds = view.OpponentSecretWorlds();
            if (!CardFrequencies.ForOrEmpty(spec.Item1).Any(f => Cards.FromId(f.CardId).IsSecret) && worlds == null) {
                Check(LethalSolver.Find(view, 2000).Outcome != LethalOutcome.Found, "empty prior must refuse lethal certification");
                continue;
            }
            Check(worlds != null && worlds.Count > 0, "class Secret hypotheses missing");
            foreach (var card in CardFrequencies.ForOrEmpty(spec.Item1).Where(f => Cards.FromId(f.CardId).IsSecret))
                Check(worlds.Any(w => w.Contains(card.CardId)), "prior Secret missing: " + card.CardId);
            Console.WriteLine("Secret worlds " + spec.Item1 + ": " + string.Join(",", worlds.Select(w => string.Join("/", w))));
        }
        var two = View(SecretBoard(CardClass.MAGE, "CORE_EX1_287", "CORE_EX1_289")).OpponentSecretWorlds();
        Check(two == null || two.All(w => w.Length == 2 && w.Distinct().Count() == 2
            && two.Any(other => other.SequenceEqual(w.Reverse()))), "multi-Secret order omitted");
    }
    public static void TestReplyScoresActualOwnerSecret()
    {
        ReplyJob Score(string id) {
            var g = Fixture(CardClass.MAGE, CardClass.WARRIOR); g.Player1.Hero.Damage = 25;
            var secret = Entity.FromCard(g.Player1, Cards.FromId(id), zone: g.Player1.HandZone);
            Check(g.Process(PlayCardTask.Spell(g.Player1, secret)), "own Secret");
            var attacker = Board(g, false, "CS2_200", true); attacker.AttackDamage = 6;
            foreach (var card in g.Player2.DeckZone.ToArray()) { g.Player2.DeckZone.Remove(card); g.Player2.SetasideZone.Add(card); }
            var job = ReplyModel.Job(View(g), NpcCatalog.All[0], 5, new DeckPlanEvaluator(DeckPlans.DragonWarrior), replyNodes: 100);
            Run(job); return job;
        }
        var barrier = Score("CORE_EX1_289"); var counter = Score("CORE_EX1_287");
        Check((barrier.Survival ?? 1) > (counter.Survival ?? 1) && counter.Survival == 0,
            "actual Secret must determine verified reply survival");
    }
    public static void TestRepeatedBounceConsumesOneOriginalCopy()
    {
        var g = new Game(new SabberStoneCore.Config.GameConfig {
            Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.ROGUE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = new[] { Cards.FromId("EX1_116") }.Concat(Enumerable.Repeat(Cards.FromId("CS2_182"), 29)).ToList(),
            StartPlayer = 1, Shuffle = false, SkipMulligan = true, History = true, Logging = false, RandomSeed = 7 });
        g.StartGame(); g.Player2.BaseMana = 10;
        Check(g.Process(EndTurnTask.Any(g.Player1)), "rogue turn");
        var leeroy = g.Player2.HandZone.Single(c => c.Card.Id == "EX1_116");
        for (int i = 0; i < 2; i++) {
            g.Player2.UsedMana = 0;
            Check(g.Process(g.Player2.Options().First(t => t.Source == leeroy)), "play Leeroy");
            var step = Entity.FromCard(g.Player2, Cards.FromId("EX1_144"), zone: g.Player2.HandZone);
            Check(g.Process(g.Player2.Options().First(t => t.Source == step && t.Target == leeroy)), "bounce Leeroy");
        }
        Check(g.Process(EndTurnTask.Any(g.Player2)), "bot turn");
        var pilot = new GoldenPilot(NpcCatalog.All[0]);
        var view = pilot.Options.FairViewFactory(g, 1, 71, DeckStyle.Tempo);
        Check(pilot.PublicInputs.OpponentPlayed.Count(c => c == "EX1_116") == 1, "replays consumed multiple physical originals");
        Check(view.Facts.Opponent.Hand.Single(c => c.CardId == "EX1_116").Cost == 3, "visible discount missing");
        var ended = view.ForReplyEndTurn();
        var job = ReplyModel.Job(view, pilot.Profile, 71, new DeckPlanEvaluator(pilot.Plan), pilot.PublicInputs.OpponentPlayed);
        Run(job);
        Check(job.Samples.All(sample => !sample.Cards.Contains("EX1_116")
            && sample.Cards.Count == ended.Facts.Opponent.Hand.Count(c => c.Unknown)), "known original sampled again");
        leeroy[GameTag.COST] = 0;
        Check(View(g).Facts.Opponent.Hand.Single(c => c.CardId == "EX1_116").Cost == 3, "hidden cost override leaked");
    }
    public static void TestKnownHandIgnoresHiddenCurrentCostAndRemembersReveal()
    {
        var g = Fixture();
        var mukla = Entity.FromCard(g.Player1, Cards.FromId("EX1_014"), zone: g.Player1.HandZone);
        Check(g.Process(g.Player1.Options().First(a => a.Source == mukla)), "Mukla");
        var banana = g.Player2.HandZone.First(); banana[GameTag.COST] = 0;
        var view = View(g);
        Check(view.Facts.Opponent.Hand.All(c => c.CardId == "EX1_014t" && c.Cost == 1), "private cost leaked into known definition");
        var revealed = Entity.FromCard(g.Player2, Cards.FromId("CS2_029"), zone: g.Player2.HandZone);
        revealed[GameTag.COST] = 2;
        revealed[GameTag.REVEALED] = 1;
        g.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(revealed));
        revealed[GameTag.REVEALED] = 0;
        Check(View(g).Facts.Opponent.Hand.Any(c => c.CardId == "CS2_029" && c.Cost == 2), "transient public reveal/visible discount forgotten");
        Check(View(g.CloneExact()).Facts.Opponent.Hand.Any(c => c.CardId == "CS2_029"), "revealed residence lost on exact clone");
        g.Player2.HandZone.Remove(revealed); g.Player2.DeckZone.Add(revealed);
        g.Player2.DeckZone.Remove(revealed); g.Player2.HandZone.Add(revealed);
        Check(View(g).Facts.Opponent.Hand.Count(c => c.CardId == "CS2_029") == 0, "hidden deck transit retained identity");
    }
    public static void TestRevealSiblingIsolation()
    {
        foreach (bool cloneFirst in new[] { false, true }) {
            var original = Fixture();
            var card = Entity.FromCard(original.Player2, Cards.FromId("CS2_029"), zone: original.Player2.HandZone);
            var sibling = original.CloneExact(history: true);
            var revealed = sibling.IdEntityDic[card.Id];
            revealed[GameTag.COST] = 2; revealed[GameTag.REVEALED] = 1;
            sibling.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(revealed));
            revealed[GameTag.REVEALED] = 0;
            if (!cloneFirst) Check(View(original).Facts.Opponent.Hand.All(c => c.Unknown), "original starts unknown");
            Check(View(sibling).Facts.Opponent.Hand.Any(c => c.CardId == "CS2_029" && c.Cost == 2), "sibling reveal missing");
            Check(View(original).Facts.Opponent.Hand.All(c => c.Unknown), "sibling reveal leaked into original");
            Check(View(original.CloneExact()).Facts.Opponent.Hand.All(c => c.Unknown), "sibling reveal leaked into cold fork");
        }
    }
    public static void TestRevealColdClone()
    {
        var original = Fixture();
        var card = Entity.FromCard(original.Player2, Cards.FromId("CS2_029"), zone: original.Player2.HandZone);
        card[GameTag.COST] = 2; card[GameTag.REVEALED] = 1;
        original.PowerHistory.Add(PowerHistoryBuilder.ShowEntity(card)); card[GameTag.REVEALED] = 0;
        Check(View(Game.LoadSnapshot(original.SaveSnapshot())).Facts.Opponent.Hand.Any(c => c.CardId == "CS2_029" && c.Cost == 2),
            "saved packet reveal lost on restore");
        var tagOnly = Fixture();
        var shown = Entity.FromCard(tagOnly.Player2, Cards.FromId("CS2_029"), zone: tagOnly.Player2.HandZone);
        shown[GameTag.COST] = 2; shown[GameTag.REVEALED] = 1; shown[GameTag.REVEALED] = 0;
        Check(View(Game.LoadSnapshot(tagOnly.SaveSnapshot())).Facts.Opponent.Hand.Any(c => c.CardId == "CS2_029" && c.Cost == 2),
            "tag-only public reveal lost on restore");
        // Emit the legacy packet-only layout (the observation map was nonserialized before version 4).
        var field = typeof(PowerHistory).GetField("_publicHandReveals", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var observations = field.GetValue(original.PowerHistory);
        field.SetValue(original.PowerHistory, new System.Collections.Generic.Dictionary<int, PowerHistoryEntity>());
        var legacy = original.SaveSnapshot();
        field.SetValue(original.PowerHistory, observations);
        Check(View(Game.LoadSnapshot(legacy)).Facts.Opponent.Hand.Any(c => c.CardId == "CS2_029" && c.Cost == 2),
            "legacy packet-only snapshot lost public reveal");
        var clone = original.CloneExact(); // No fair view/cache warmup on original before the fork.
        Check(View(Game.LoadSnapshot(clone.SaveSnapshot())).Facts.Opponent.Hand.Any(c => c.CardId == "CS2_029" && c.Cost == 2), "exact clone snapshot lost reveal");
        Check(View(clone).Facts.Opponent.Hand.Any(c => c.CardId == "CS2_029" && c.Cost == 2), "cold clone lost public reveal");
        var hidden = clone.IdEntityDic[card.Id]; hidden[GameTag.COST] = 0;
        Check(View(clone).Facts.Opponent.Hand.Single(c => c.CardId == "CS2_029").Cost == 2, "hidden cost changed observation");
    }
    public static void TestOffClassSecretCannotCertifyLethal()
    {
        var g = SecretBoard(CardClass.HUNTER, "CORE_EX1_287");
        var view = View(g);
        var fireball = view.Actions.Single(a => a.Type == PlayerTaskType.PLAY_CARD && a.Target == view.Facts.Opponent.Hero.EntityId);
        var verify = LethalJob.Verify(view, new[] { fireball }, 2000); Run(verify);
        var replay = g.CloneExact(); Check(replay.Process(fireball.Resolve(replay.Player1.Options())), "authoritative Fireball");
        Check(replay.Player2.Hero.Health == 6 && replay.State != State.COMPLETE, "Counterspell fixture must stop lethal");
        Check(verify.Result.Outcome != LethalOutcome.Found, "off-class Counterspell falsely certified: " + verify.Result.Nodes);
    }
    public static void TestSecretWithoutPublicPlayCannotCertifyLethal()
    {
        var g = Fixture(CardClass.MAGE, CardClass.HUNTER);
        var secret = Entity.FromCard(g.Player2, Cards.FromId("CORE_EX1_287"), zone: g.Player2.SecretZone);
        secret.Card.Power.Trigger.Activate(secret);
        g.Player2.Hero.Damage = 24;
        Entity.FromCard(g.Player1, Cards.FromId("CS2_029"), zone: g.Player1.HandZone);
        var view = View(g);
        var fireball = view.Actions.Single(a => a.Type == PlayerTaskType.PLAY_CARD && a.Target == view.Facts.Opponent.Hero.EntityId);
        var verify = LethalJob.Verify(view, new[] { fireball }, 2000); Run(verify);
        var replay = g.CloneExact(); Check(replay.Process(fireball.Resolve(replay.Player1.Options())), "direct Secret authoritative replay");
        Check(replay.Player2.Hero.Health == 6 && replay.State != State.COMPLETE, "direct Counterspell stops lethal");
        Check(verify.Result.Outcome != LethalOutcome.Found, "unexplained Secret install falsely certified");
    }
    public static void TestRedactedEntityZeroCannotExplainSecretGeneration()
    {
        var g = SecretBoard(CardClass.HUNTER, "CORE_EX1_287");
        var wisp = Entity.FromCard(g.Player1, Cards.FromId("CS2_231"), zone: g.Player1.HandZone);
        Check(g.Process(g.Player1.Options().First(t => t.Source == wisp)), "public Wisp play");
        g.Player1.DeckZone.Add(g.Player1.BoardZone.Remove((Minion)wisp));
        var view = View(g);
        Check(view.FingerprintGame.HistoryFacts.Query.Events<HistoryCardFact>().Any(f => f.EntityId == 0 && f.DefinitionKnown),
            "fixture needs a public definition with an erased entity link");
        var fireball = view.Actions.Single(a => a.Type == PlayerTaskType.PLAY_CARD && a.Target == g.Player2.Hero.Id);
        var verify = LethalJob.Verify(view, new[] { fireball }, 2000); Run(verify);
        Check(verify.Result.Outcome != LethalOutcome.Found, "unrelated erased ID explained away generated Counterspell");
    }
    public static void TestCappedReplyStopsAtExtraTurnBoundary()
    {
        var g = Fixture(CardClass.MAGE, CardClass.WARRIOR);
        var secret = Entity.FromCard(g.Player1, Cards.FromId("CORE_EX1_287"), zone: g.Player1.HandZone);
        Check(g.Process(PlayCardTask.Spell(g.Player1, secret)), "own Counterspell");
        Board(g, false, "CS2_200", true);
        SabberStoneCore.Actions.TurnQueueRules.Prepend(g, g.Player2, g.Player2);
        var job = ReplyModel.Job(View(g), NpcCatalog.All[0], 5, new DeckPlanEvaluator(DeckPlans.DragonWarrior), replyNodes: 1);
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var replayField = typeof(ReplyJob).GetField("actualReply", flags);
        var lineField = typeof(ReplyJob).GetField("actualLine", flags);
        var gameField = typeof(FairBotState).GetField("game", flags);
        int maxTurn = 0; bool cappedAttack = false, settlementWitness = false;
        for (int n = 0; n < 1000 && !job.IsDone; n++) {
            job.StepNode();
            var line = lineField.GetValue(job) as System.Collections.Generic.IReadOnlyList<BotAction>;
            if (line != null) cappedAttack |= line.Any(a => a.Type == PlayerTaskType.MINION_ATTACK) && line.Count(a => a.Type == PlayerTaskType.END_TURN) == 1;
            var state = replayField.GetValue(job) as FairBotState;
            if (state != null) {
                maxTurn = Math.Max(maxTurn, ((Game)gameField.GetValue(state)).Turn);
                if (((Game)gameField.GetValue(state)).Turn == g.Turn + 2 && typeof(ReplyJob).GetField("actualWitness", flags) != null) {
                    var witness = (System.Collections.Generic.List<BotAction>)typeof(ReplyJob).GetField("actualWitness", flags).GetValue(job);
                    settlementWitness |= witness.Last().Type == PlayerTaskType.END_TURN;
                    var root = (FairBotState)typeof(ReplyJob).GetField("actualRoot", flags).GetValue(job);
                    foreach (var action in witness) root = root.Apply(action);
                    Check(root.FingerprintGame.HistoryFacts.CanonicalText() == state.FingerprintGame.HistoryFacts.CanonicalText(),
                        "settlement witness does not reproduce scored endpoint");
                }
            }
        }
        Check(job.IsDone && cappedAttack, "capped reply must replay attack prefix with recorded settlement");
        Check(maxTurn == g.Turn + 2, "fallback skipped extra turn: last turn=" + maxTurn);
        Check(settlementWitness, "fallback END_TURN missing from verification witness");
        Check(job.Nodes <= ReplyModel.NodeAllowance(job.Samples.Count, 1, true), "reply exceeded allowance");
    }

    public static void TestPublicGeneratorRefusesSecretCoverage()
    {
        var g = SecretBoard(CardClass.HUNTER, "EX1_610");
        Check(View(g).OpponentSecretWorlds() != null, "class-only control needs coverage");
        var generator = Entity.FromCard(g.Player1, Cards.FromId("TIME_860"), zone: g.Player1.HandZone);
        Check(g.Process(g.Player1.Options().First(t => t.Source == generator)), "play public Faceless Enigma");
        Check(g.Player1.Choice != null, "Enigma offers");
        var decision = g.GetPendingDecisions().Single();
        Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId,
            new[] { g.Player1.Choice.Choices.First() }), out var error), "Enigma selection: " + error);
        Check(View(g).OpponentSecretWorlds() == null, "Enigma public generator must fail closed");
        // Public source remains relevant even after it leaves play and the latest Secret is swapped invisibly.
        var source = g.Player1.BoardZone.Single(c => c.Id == generator.Id);
        g.Player1.SetasideZone.Add(g.Player1.BoardZone.Remove(source));
        Check(View(g.CloneExact()).OpponentSecretWorlds() == null, "generator history lost on fork");
    }
    public static void TestHunterSecretHistoryStillTakesLethal()
    {
        foreach (bool heroPower in new[] { false, true }) {
            var g = SecretBoard(CardClass.HUNTER, "EX1_610");
            var plausibleSecrets = View(g).OpponentSecretWorlds();
            Check(plausibleSecrets != null && plausibleSecrets.Count > 0, "known Hunter Secret pool");
            Check(g.Process(EndTurnTask.Any(g.Player1)), "opponent action turn");
            if (heroPower) Check(g.Process(HeroPowerTask.Any(g.Player2)), "opponent hero power");
            else {
                var coin = Entity.FromCard(g.Player2, Cards.FromId("GAME_005"), zone: g.Player2.HandZone);
                Check(g.Process(PlayCardTask.Spell(g.Player2, coin)), "opponent Coin");
            }
            Check(g.Process(EndTurnTask.Any(g.Player2)), "lethal turn");
            var pilot = new GoldenPilot(NpcCatalog.All[0]);
            var view = pilot.Options.FairViewFactory(g, 1, 71, DeckStyle.Tempo);
            Check(view.OpponentSecretWorlds() != null, "Coin/hero power erased bounded coverage");
            var lethal = LethalSolver.Find(view, 2000);
            Check(lethal.Outcome == LethalOutcome.Found, "ordinary Hunter history must verify");
            var result = pilot.Decide(view, new GoldenContext(g.Turn, 0, 71, pilot.Profile));
            Check(result.Action.Type == PlayerTaskType.PLAY_CARD && result.Action.Source == g.Player1.HandZone.Single(c => c.Card.Id == "CS2_029").Id
                && result.Action.Target == g.Player2.Hero.Id, "normal search missed Hunter lethal: " + result.Action);
            Check(result.Value >= 999000, "exhaustively replayed lethal lost certification");
            foreach (var world in plausibleSecrets) {
                var replay = view.WithOpponentSecrets(world, 71).Apply(result.Action);
                Check(replay.Facts.Own.PlayState == PlayState.WON, "plausible Secret stopped lethal: " + string.Join(",", world));
            }
            Check(g.Process(result.Action.Resolve(g.Player1.Options())) && g.Player1.PlayState == PlayState.WON,
                "chosen lethal failed authoritative replay");
        }
    }

    public static void TestMageSecretProbeAfterHeroPower()
    {
        var g = SecretBoard(CardClass.MAGE, "CORE_EX1_287");
        Check(g.Process(HeroPowerTask.Any(g.Player1, g.Player2.Hero)), "public hero power");
        Check(g.Player2.Hero.Health == 5 && g.Player1.RemainingMana == 8, "reviewer position");
        var pilot = new GoldenPilot(NpcCatalog.All[0]);
        var view = pilot.Options.FairViewFactory(g, 1, 71, DeckStyle.Tempo);
        var coin = view.Actions.Single(a => a.Source == g.Player1.HandZone.Single(c => c.Card.Id == "GAME_005").Id);
        var fireball = view.Actions.Single(a => a.Type == PlayerTaskType.PLAY_CARD && a.Target == g.Player2.Hero.Id);
        var control = g.CloneExact();
        Check(control.Process(coin.Resolve(control.Player1.Options())) && control.Process(fireball.Resolve(control.Player1.Options()))
            && control.Player1.PlayState == PlayState.WON, "Coin then Fireball must win");
        var result = pilot.Decide(view, new GoldenContext(g.Turn, 0, 71, pilot.Profile));
        Check(result.Action.Equals(coin), "fake lethal beat winning probe: " + result.Action + " value=" + result.Value);
        Check(g.Process(result.Action.Resolve(g.Player1.Options())), "chosen probe");
        view = pilot.Options.FairViewFactory(g, 1, 71, DeckStyle.Tempo);
        result = pilot.Decide(view, new GoldenContext(g.Turn, 1, 71, pilot.Profile));
        Check(g.Process(result.Action.Resolve(g.Player1.Options())) && g.Player1.PlayState == PlayState.WON,
            "probe did not lead to authoritative lethal");
    }

    public static void TestHeroPowerHistoryKeepsSecretCoverage()
    {
        var g = SecretBoard(CardClass.HUNTER, "EX1_610");
        Check(g.Process(HeroPowerTask.Any(g.Player1, g.Player2.Hero)), "public hero power");
        var view = View(g);
        Check(view.OpponentSecretWorlds() != null, "ordinary hero power erased bounded Secret worlds");
        var fireball = view.Actions.Single(a => a.Type == PlayerTaskType.PLAY_CARD && a.Target == g.Player2.Hero.Id);
        var verify = LethalJob.Verify(view, new[] { fireball }, 2000); Run(verify);
        Check(verify.Result.Outcome == LethalOutcome.Found, "no plausible Hunter Secret stops Fireball");
    }

    private static FairBotState DiscoverSecretRoot()
    {
        var g = SecretBoard(CardClass.HUNTER, "EX1_610");
        g.Player2.Hero.Damage = 22; g.Player1.BaseMana = 5;
        Entity.FromCard(g.Player1, Cards.FromId("FIR_939"), zone: g.Player1.HandZone);
        return View(g);
    }
    private static void CheckSecretDiscoverChoice(int position)
    {
        var root = DiscoverSecretRoot();
        var cast = root.Actions.Single(a => a.Source == root.Facts.Own.Hand.Single(c => c.CardId == "FIR_939").EntityId
            && a.Target == root.Facts.Opponent.Hero.EntityId);
        var search = root.WithArtificialSeed(1).Apply(cast);
        var replay = root.WithOpponentSecrets(new[] { "CORE_EX1_289" }, 1).Apply(cast);
        var chooser = search.FingerprintGame.Player1;
        int id = chooser.Choice.Choices[position];
        var choice = BotAction.From(ChooseTask.Pick(chooser, id));
        string expected = search.FingerprintGame.IdEntityDic[id].Card.Id;
        Check(replay.Actions.Contains(choice), "Secret allocation removed Discover choice " + id);
        Check(replay.FingerprintGame.IdEntityDic[id].Card.Id == expected,
            "Secret allocation rebound overlapping choice " + id + " from " + expected + " to " + replay.FingerprintGame.IdEntityDic[id].Card.Id);
        replay = replay.Apply(choice);
        search = search.Apply(choice);
        Check(replay.Facts.Own.Hand.Select(c => c.CardId).SequenceEqual(search.Facts.Own.Hand.Select(c => c.CardId)),
            "replay selected a different Discover card (including its Shatter parts)");
    }
    public static void TestSecretDiscoverMissingChoiceId() => CheckSecretDiscoverChoice(0);
    public static void TestSecretDiscoverOverlappingChoiceId() => CheckSecretDiscoverChoice(1);

    public static void TestSecretDiscoverContinuationCertifies()
    {
        var root = DiscoverSecretRoot();
        var state = root;
        var line = new System.Collections.Generic.List<BotAction>();
        foreach (string card in new[] { "FIR_939", null, "GAME_005", "CS2_029" }) {
            var action = card == null ? state.Actions.First(a => a.Type == PlayerTaskType.CHOOSE)
                : state.Actions.First(a => a.Source == state.Facts.Own.Hand.Single(c => c.CardId == card).EntityId
                    && (card == "GAME_005" || a.Target == state.Facts.Opponent.Hero.EntityId));
            line.Add(action); state = state.Apply(action);
        }
        Check(state.Facts.Own.PlayState == PlayState.WON, "Suffusion/choice/Coin/Fireball control must win");
        var verify = LethalJob.Verify(root, line, 2000); Run(verify);
        Check(verify.Result.Outcome == LethalOutcome.Found, "Discover continuation lost Secret certificate: "
            + verify.Result.Outcome + " after " + verify.Result.Nodes + " nodes");
    }

    public static void TestRefutedSecretReplayStopsImmediately()
    {
        var root = View(SecretBoard(CardClass.MAGE, "CORE_EX1_287"));
        var fireball = root.Actions.Single(a => a.Type == PlayerTaskType.PLAY_CARD && a.Target == root.Facts.Opponent.Hero.EntityId);
        bool refuted = false;
        var verify = LethalJob.Verify(root, new[] { fireball }, 2000, state => {
            if (!state.Facts.Own.Hand.Any(c => c.CardId == "CS2_029") && state.Facts.Opponent.Hero.Health == 6) refuted = true;
            return state.Evaluate();
        });
        while (!verify.IsDone && !refuted) verify.StepNode();
        Check(refuted, "fixture must replay a blocking Secret");
        Check(verify.IsDone && verify.Result.Outcome == LethalOutcome.None,
            "disproven Secret line kept spending nodes on unused worlds");
        Check(verify.SecretHeuristicValue.HasValue && verify.SecretHeuristicValue < 10000,
            "early refutation lost bounded heuristic evidence");
    }

    public static void TestSecretHistoryRetainsEndTurnLethalWitness()
    {
        var g = SecretBoard(CardClass.HUNTER, "EX1_610");
        Check(g.Process(HeroPowerTask.Any(g.Player1, g.Player2.Hero)), "public hero power");
        foreach (var card in g.Player1.HandZone.ToArray()) g.Player1.SetasideZone.Add(g.Player1.HandZone.Remove(card));
        Board(g, true, "EX1_298");
        var view = View(g);
        Check(view.OpponentSecretWorlds() != null, "end-turn fixture lost coverage");
        var job = TurnPlanner.Job(view, NpcCatalog.All[0], DeckPlans.DragonWarrior, 71, 1926);
        Run(job);
        Check(job.Result.Line.Count > 0 && job.Result.Line.Last().Type == PlayerTaskType.END_TURN,
            "projected lethal lost END_TURN witness");
        Check(job.Result.Value >= 999000, "unopposed projected lethal lost certification");
        foreach (var action in job.Result.Line) Check(g.Process(action.Resolve(g.Player1.Options())), "projected witness replay");
        Check(g.Player1.PlayState == PlayState.WON, "projected witness did not win");
    }

    public static void TestIncompleteSecretCoverageDoesNotPromoteLuckyLethal()
    {
        var g = SecretBoard(CardClass.HUNTER, "EX1_610");
        foreach (var card in g.Player1.HandZone.ToArray()) g.Player1.SetasideZone.Add(g.Player1.HandZone.Remove(card));
        var missiles = Entity.FromCard(g.Player1, Cards.FromId("EX1_277"), zone: g.Player1.HandZone);
        Entity.FromCard(g.Player1, Cards.FromId("CS2_026"), zone: g.Player1.HandZone);
        g.Player1.BaseMana = 3; g.Player1.Hero.Damage = 24; g.Player2.Hero.Damage = 27;
        Board(g, false, "CS2_200");
        while (g.HistoryFacts.RecordCount <= 8192) {
            var card = g.Player1.DeckZone.First();
            g.Player1.SetasideZone.Add(g.Player1.DeckZone.Remove(card));
            g.Player1.DeckZone.Add(g.Player1.SetasideZone.Remove(card));
        }
        for (int seed = 1; seed <= 100; seed++) {
            var pilot = new GoldenPilot(NpcCatalog.All[0]);
            var view = pilot.Options.FairViewFactory(g, 1, seed, DeckStyle.Tempo);
            var cast = view.Actions.Single(a => a.Source == missiles.Id);
            if (view.Apply(cast).Facts.Own.PlayState != PlayState.WON) continue;
            Check(view.OpponentSecretWorlds() == null, "oversized history must retain Incomplete");
            Check(Enumerable.Range(1, 4).Any(s => view.WithArtificialSeed(s).Apply(cast).Facts.Own.PlayState != PlayState.WON),
                "fixture needs a disproving artificial seed");
            var result = pilot.Decide(view, new GoldenContext(g.Turn, 0, seed, pilot.Profile));
            Check(result.Value < 10000 && result.Candidates.All(c => c.Value < 10000),
                "incomplete random sample gained near-terminal priority: " + result.Value);
            return;
        }
        throw new Exception("fixture never found lucky missiles seed");
    }

    public static void TestSecretWorldOverflowReplaysWithinBudget()
    {
        var g = SecretBoard(CardClass.MAGE, "CORE_EX1_287", "CORE_EX1_289", "CORE_BAR_812", "CORE_LOOT_101");
        var view = View(g);
        Check(view.OpponentSecretWorlds() == null, "four Mage Secrets exceed exhaustive world cap");
        var coin = view.Actions.Single(a => a.Source == g.Player1.HandZone.Single(c => c.Card.Id == "GAME_005").Id);
        var fireball = view.Actions.Single(a => a.Type == PlayerTaskType.PLAY_CARD && a.Target == g.Player2.Hero.Id);
        var verify = LethalJob.Verify(view, new[] { coin, fireball }, 64); Run(verify);
        Check(verify.Result.Outcome == LethalOutcome.Incomplete && verify.Result.Nodes > 1 && verify.Result.Nodes <= 64,
            "overflow skipped replay or escaped budget: " + verify.Result.Outcome + "/" + verify.Result.Nodes);
    }

    public static void TestRootHistoryWorkCaps()
    {
        foreach (bool packets in new[] { false, true }) {
            var g = Fixture();
            if (packets) {
                while (g.PowerHistory.Full.Count <= 65536)
                    g.PowerHistory.Add(PowerHistoryBuilder.TagChange(g.Player1.Id, GameTag.RESOURCES_USED, 0));
            } else {
                var card = g.Player1.DeckZone.First();
                while (g.HistoryFacts.Records.Count() <= 8192) {
                    g.Player1.SetasideZone.Add(g.Player1.DeckZone.Remove(card));
                    g.Player1.DeckZone.Add(g.Player1.SetasideZone.Remove(card));
                }
            }
            var reveal = Entity.FromCard(g.Player2, Cards.FromId("CS2_029"), zone: g.Player2.HandZone);
            reveal[GameTag.REVEALED] = 1; reveal[GameTag.REVEALED] = 0;
            var attacker = Board(g, true, "CS2_200", true); attacker.AttackDamage = 30;
            var before = g.SaveSnapshot();
            int logs = 0; var oldLog = FairBotState.HistoryLimitLog;
            FairBotState.HistoryLimitLog = _ => logs++;
            try {
            for (int repeat = 0; repeat < 2; repeat++) {
                var view = View(g);
                Check(view.Facts.Opponent.Hand.All(c => c.Unknown), "over-cap hand knowledge not degraded");
                var pilot = new GoldenPilot(NpcCatalog.All[0]);
                var result = pilot.Decide(view, new GoldenContext(g.Turn, 0, 71, pilot.Profile));
                Check(result.Action.Type == PlayerTaskType.MINION_ATTACK && result.Action.Target == g.Player2.Hero.Id,
                    "history cap prevented lethal: " + result.Action);
            }
            } finally { FairBotState.HistoryLimitLog = oldLog; }
            Check(logs == 1, "cap must log once per authoritative game");
            Check(before.SequenceEqual(g.SaveSnapshot()), "root degradation mutated authoritative state");
        }
    }

}
