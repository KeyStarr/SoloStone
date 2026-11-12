using System.Diagnostics;
using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Rewind;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static partial class RewindProof
{
    private static void Check(bool ok, string message) => Core2026Proof.Check(ok, "Rewind: " + message);
    private static Game Fixture(int seed = 20) => PinnedPoolFixture.Use(PlayerDecisionProof.Fixture(seed));
    private static long Counter(Game g, string name) => Convert.ToInt64(typeof(Game).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(g));
    private static void Answer(Game g, bool rewind)
    {
        var t = g.Rewind;
        Check(t?.Stage == RewindStage.Offered, "offer exists");
        var decision = g.GetPendingDecisions().Single();
        Check(decision.Id == t.OfferChoiceId && decision.SourceId == t.SourceId, "offer identity/source");
        Check(g.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId,
            new[] { rewind ? t.RewindEntityId : t.KeepEntityId }), out var error), "answer accepted: " + error);
    }
    private static void Reject(Game g, DecisionAnswer answer)
    {
        var bytes = g.SaveSnapshot();
        Check(!g.TryAnswer(answer, out _) && bytes.SequenceEqual(g.SaveSnapshot()), "rejected answer changes zero snapshot bytes");
    }
    private static void Accounting(Game g, int id, int cost)
    {
        var p = g.Player1;
        Check(p.UsedMana == cost && p.NumCardsPlayedThisTurn == 1 && p.PlayHistory.Count == 1 &&
            g.HistoryFacts.Query.Plays().Count(f => f.EntityId == id) == 1, "single net mana/payment/play fact/counter");
        if (cost == 3) Check(p.NumSpellsPlayedThisGame == 1, "one player spell cast counter");
        Check(g.Game == g && p.Game == g && g.Player2.Game == g && g.IdEntityDic.All(e => e.Value.Game == g && e.Value.Controller.Game == g),
            "in-place root owns entire restored graph");
    }
    private static void Balanced(Game g)
    {
        int depth = 0;
        foreach (var e in g.PowerHistory.Full)
        {
            if (e is PowerHistoryBlockStart) depth++;
            if (e is PowerHistoryBlockEnd) depth--;
            Check(depth >= 0, "no unmatched history end");
        }
        Check(depth == 0 && g.Resolution.Depth == 0 && g.TaskQueue.IsQuiescent, "balanced quiescent boundary");
    }
    private static void Trees()
    {
        foreach (string card in new[] { "TIME_001", "TIME_038" })
        foreach (int keepAt in Enumerable.Range(0, card == "TIME_001" ? 2 : 4))
        {
            // Seed 1 avoids Morchie across retries in the pinned catalog.
            var g = Fixture(seed: 1); var source = Play(g, g.Player1, card); int cost = source.Card.Cost;
            var history = g.PowerHistory;
            for (int used = 0; used < keepAt; used++)
            {
                var transaction = g.Rewind;
                Check(!transaction.KeepBoth, "normal retry fixture excludes KeepBoth");
                var cursor = g.Random.CaptureState();
                long decision = transaction.OfferChoiceId;
                long entity = Counter(g, "_idIndex"), oop = Counter(g, "_oopIndex");
                int high = g.IdEntityDic.Max(p => p.Key);
                Answer(g, true);
                Check(ReferenceEquals(history, g.PowerHistory), "live history object carried");
                Check(PinnedPoolFixture.LoadSnapshot(transaction.Baseline).Random.CaptureState().Equals(cursor), "attempt end cursor equals replay start cursor");
                Check(Counter(g, "_nextDecisionId") > decision && g.GetPendingDecisions().Single().Id > decision &&
                    Counter(g, "_idIndex") >= entity && Counter(g, "_oopIndex") >= oop, "decision/entity/oop high-water never lowers");
                var last = g.PowerHistory.Last;
                int reset = last.FindIndex(e => e is PowerHistoryResetGame);
                Check(reset > 0 && last[reset - 1] is PowerHistoryBlockStart b && b.BlockType == BlockType.GAME_RESET && b.Source == source.Id,
                    "reset remains in Last before replay");
                var full = last.Skip(reset + 1).TakeWhile(e => e is PowerHistoryFullEntity).Cast<PowerHistoryFullEntity>().Select(e => e.Entity.Id).ToArray();
                Check(full.SequenceEqual(full.Order()) && full.All(id => id < high - 1) &&
                    last[reset + full.Length + 1] is PowerHistoryBlockEnd, "reset FULL_ENTITY snapshots sorted in one block");
                if (card == "TIME_038")
                {
                    Check(g.IdEntityDic[source.Id].Card.Id == "TIME_038t" + (used + 1) &&
                        g.IdEntityDic[source.Id][GameTag.USED_REWIND] == used + 1, "Clocksworth variant/USED_REWIND progression");
                    Check(g.Player1.BoardZone.Where(m => m.Id != source.Id).All(m => m.Id > high), "replay summons use fresh entity IDs");
                }
                Check(g.PowerHistory.Full.Count(e => e is PowerHistoryResetGame) == used + 1, "history audit keeps every reset");
                Reject(g, new DecisionAnswer(decision, g.Player1.Id, new[] { high }));
                Accounting(g, source.Id, cost); Balanced(g);
            }
            if (g.Rewind != null) Answer(g, false);
            Check(g.Rewind == null && g.Player1.Choice == null, "Keep or exhausted budget ends transaction");
            if (card == "TIME_001") Check(g.Player2.Hero.Damage == 6, "only final spell outcome remains");
            else Check(g.Player1.BoardZone.Count == 3, "only final two summons remain");
            Accounting(g, source.Id, cost); Balanced(g);
        }
    }
    private static void InvalidInputs()
    {
        var g = Fixture(seed: 1); Play(g, g.Player1, "TIME_038"); var t = g.Rewind;
        var good = new DecisionAnswer(t.OfferChoiceId, t.ControllerId, new[] { t.RewindEntityId });
        Reject(g, null);
        Reject(g, new DecisionAnswer(t.OfferChoiceId, g.Player2.Id, new[] { t.RewindEntityId }));
        Reject(g, new DecisionAnswer(t.OfferChoiceId - 1, t.ControllerId, new[] { t.KeepEntityId }));
        Reject(g, new DecisionAnswer(t.OfferChoiceId, t.ControllerId, Array.Empty<int>()));
        Reject(g, new DecisionAnswer(t.OfferChoiceId, t.ControllerId, new[] { t.KeepEntityId, t.KeepEntityId }));
        Reject(g, new DecisionAnswer(t.OfferChoiceId, t.ControllerId, new[] { t.SourceId }));
        var card = g.Player1.HandZone.FirstOrDefault() ?? Add(g.Player1, "CS2_182");
        foreach (var task in new PlayerTask[] { EndTurnTask.Any(g.Player1), PlayCardTask.Any(g.Player1, card),
            HeroPowerTask.Any(g.Player1), ChooseTask.Pick(g.Player2, t.KeepEntityId), ChooseTask.Pick(g.Player1, t.SourceId) })
        {
            var bytes = g.SaveSnapshot();
            Check(!g.Process(task) && bytes.SequenceEqual(g.SaveSnapshot()), "only valid answer or Concede accepted while Offered");
        }
        Answer(g, true); Reject(g, good);
        var second = g.Rewind; Answer(g, false);
        Reject(g, new DecisionAnswer(second.OfferChoiceId, second.ControllerId, new[] { second.KeepEntityId }));
        g = Fixture(); Play(g, g.Player1, "TIME_001");
        Check(g.Process(ConcedeTask.Any(g.Player2)) && g.Rewind == null && g.Player1.Choice == null, "Concede drops offered transaction");
    }
    private static string Future(Game g)
    {
        var history = new List<string>();
        while (g.Rewind != null)
        {
            Answer(g, true);
            history.Add(Canonical.Value(g.PowerHistory.Last));
        }
        End(g);
        return Canonical.State(g) + Canonical.Value(g.Random.CaptureState()) + string.Join("\n", history);
    }
    private static void Persistence()
    {
        foreach (string card in new[] { "TIME_001", "TIME_038" })
        foreach (int used in Enumerable.Range(0, card == "TIME_001" ? 1 : 3))
        {
            var g = Fixture(seed: 1); Play(g, g.Player1, card);
            for (int i = 0; i < used; i++) Answer(g, true);
            var bytes = g.SaveSnapshot(); var loaded = PinnedPoolFixture.LoadSnapshot(bytes); var clone = g.CloneExact(history: true);
            Check(bytes.SequenceEqual(loaded.SaveSnapshot()), "mid-offer/mid-chain byte round trip");
            Check(!ReferenceEquals(g.Rewind, clone.Rewind) && ReferenceEquals(g.Rewind.Baseline, clone.Rewind.Baseline), "exact clone copies values, shares immutable baseline bytes");
            Check(Future(g) == Future(loaded) && Future(clone) == Future(PinnedPoolFixture.LoadSnapshot(bytes)), "source/load/exact-clone identical future");
        }
    }
    internal static void Child(string path)
    {
        var bytes = File.ReadAllBytes(path); var g = PinnedPoolFixture.LoadSnapshot(bytes);
        Check(bytes.SequenceEqual(g.SaveSnapshot()) && Future(g) == File.ReadAllText(path + ".future"), "fresh process offer future");
    }
    private static void FreshProcess()
    {
        string path = Path.Combine(Path.GetTempPath(), "hs-rewind-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (int used in new[] { 0, 1, 2 })
            {
                var g = Fixture(seed: 1); Play(g, g.Player1, "TIME_038");
                for (int i = 0; i < used; i++) Answer(g, true);
                File.WriteAllBytes(path, g.SaveSnapshot()); File.WriteAllText(path + ".future", Future(g.CloneExact(history: true)));
                var start = new ProcessStartInfo(Environment.ProcessPath) { RedirectStandardOutput = true, RedirectStandardError = true };
                if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
                start.ArgumentList.Add("--rewind-child"); start.ArgumentList.Add(path);
                using var child = Process.Start(start);
                string output = child.StandardOutput.ReadToEnd(), error = child.StandardError.ReadToEnd();
                Check(child.WaitForExit(30000) && child.ExitCode == 0, "fresh-process restore " + used + ": " + output + error);
            }
        }
        finally { File.Delete(path); File.Delete(path + ".future"); }
    }
    private static void SimulationAndDormancy()
    {
        var g = Fixture(); var card = Add(g.Player1, "TIME_001"); var bytes = g.SaveSnapshot();
        Check(RewindRules.WouldBegin(g, PlayCardTask.Any(g.Player1, card)) && bytes.SequenceEqual(g.SaveSnapshot()), "WouldBegin is pure");
        var simulation = g.CloneForSimulation(44, history: true);
        Check(simulation.RewindDisabled && simulation.Rewind == null && !RewindRules.WouldBegin(simulation,
            PlayCardTask.Any(simulation.Player1, simulation.IdEntityDic[card.Id])), "simulation disables begin");
        Check(simulation.Process(PlayCardTask.Any(simulation.Player1, simulation.IdEntityDic[card.Id])) && simulation.Rewind == null && simulation.Player1.Choice == null,
            "simulation play behaves as Keep");
        PlayEntity(g, g.Player1, card); simulation = g.CloneForSimulation(44);
        Check(simulation.Rewind == null && simulation.Player1.Choice == null && simulation.CloneExact().RewindDisabled,
            "simulation from offer drops transaction and choice; descendants stay disabled");
        var ordinary = Fixture(); var disabled = ordinary.CloneForSimulation(ordinary.Seed, history: true);
        disabled.Random.RestoreState(ordinary.Random.CaptureState());
        foreach (var game in new[] { ordinary, disabled }) Play(game, game.Player1, "CS2_182");
        Check(Canonical.State(ordinary) == Canonical.State(disabled) && Canonical.Value(ordinary.PowerHistory.Last) == Canonical.Value(disabled.PowerHistory.Last),
            "ordinary action byte-identical state/history with Rewind dormant");
        Check(ordinary.Rewind == null && ordinary.CloneExact().Rewind == null, "no-Rewind snapshots/clones remain dormant");
        g = Fixture(); var spell = (Spell)Entity.FromCard(g.Player1, Cards.FromId("TIME_001"));
        Generic.CastSpell(g.Player1, g, spell, null, 0); g.DeathProcessingAndAuraUpdate();
        Check(g.Rewind == null && g.Player1.Choice == null, "indirect cast never starts transaction");
        g = Fixture(); Summon(g.Player1, "TIME_038");
        Check(g.Rewind == null, "generated summon never starts transaction");
    }
    private static void KeepBoth()
    {
        foreach (string card in new[] { "TIME_001", "TIME_038" })
        {
            var g = Fixture(); QuestRules.AddPlayerEffect(g.Player1, g.Player1.Hero, "END_036e");
            var source = Play(g, g.Player1, card); Check(g.Rewind.KeepBoth, "controller enchantment fixes KeepBoth at offer");
            int id = source.Id; var zone = source.Zone; int cost = source.Card.Cost;
            var bytes = g.SaveSnapshot(); var load = PinnedPoolFixture.LoadSnapshot(bytes); var clone = g.CloneExact(history: true);
            foreach (var game in new[] { g, load, clone }) Answer(game, true);
            Check(Canonical.State(g) == Canonical.State(load) && Canonical.State(g) == Canonical.State(clone), "KeepBoth load/clone future parity");
            Check(g.Rewind == null && ReferenceEquals(source, g.IdEntityDic[id]) && source.Zone == zone &&
                g.PowerHistory.Full.All(e => e is not PowerHistoryResetGame), "KeepBoth keeps graph, source and both outcomes; no reset");
            if (card == "TIME_001") Check(g.Player2.Hero.Damage == 12, "KeepBoth repeats spell damage once");
            else Check(g.Player1.BoardZone.Count == 5, "KeepBoth repeats Battlecry once");
            Accounting(g, id, cost); Balanced(g);
        }
    }
    private static void Lethal()
    {
        foreach (bool keepBoth in new[] { false, true })
        {
            var g = Fixture(); g.Player2.Hero.Damage = keepBoth ? 19 : 26;
            if (keepBoth) QuestRules.AddPlayerEffect(g.Player1, g.Player1.Hero, "END_036e");
            Play(g, g.Player1, "TIME_001");
            if (keepBoth) Answer(g, true);
            Check(g.State == State.COMPLETE && g.Rewind == null && g.Player1.Choice == null && g.GetPendingDecisions().Count == 0,
                "lethal attempt or KeepBoth repeat ends game without offer");
            Balanced(g);
        }
    }
    private static void NestedDecision()
    {
        // Reuse installed resumable spell adapter while giving only this source the own Rewind tag.
        var g = Fixture(); var source = Add(g.Player1, "TIME_770"); source[GameTag.REWIND] = 1;
        PlayEntity(g, g.Player1, source);
        Check(g.Rewind?.Stage == RewindStage.Attempt && g.Resolution.HasSuspendedEffect, "attempt spans nested decision");
        var bytes = g.SaveSnapshot(); var load = PinnedPoolFixture.LoadSnapshot(bytes); var clone = g.CloneExact(history: true);
        foreach (var game in new[] { g, load, clone })
        {
            var decision = game.GetPendingDecisions().Single();
            Check(game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { decision.Options.First().OptionId }), out _), "nested answer");
            Check(game.Rewind?.Stage == RewindStage.Offered && !game.Resolution.HasSuspendedEffect, "offer waits for resumed play tail");
            Answer(game, true);
            Check(game.Rewind?.Stage == RewindStage.Attempt && game.Resolution.HasSuspendedEffect, "replay reopens nested decision");
            var finalAttempt = game.SaveSnapshot();
            Check(finalAttempt.SequenceEqual(PinnedPoolFixture.LoadSnapshot(finalAttempt).SaveSnapshot()), "exhausted replay nested decision persists");
            decision = game.GetPendingDecisions().Single();
            Check(game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { decision.Options.Last().OptionId }), out _), "replay nested answer");
            Check(game.Rewind == null && game.Player1.NumCardsPlayedThisTurn == 1 && game.Player1.UsedMana == 4,
                $"nested tails and payment only once net: stage={game.Rewind?.Stage}, cards={game.Player1.NumCardsPlayedThisTurn}, mana={game.Player1.UsedMana}, suspended={game.Resolution.HasSuspendedEffect}");
        }
        Check(Canonical.State(g) == Canonical.State(load) && Canonical.State(g) == Canonical.State(clone), "nested source/load/clone parity");
    }
    private static void SizeAndTime()
    {
        var g = Fixture(seed: 1); Play(g, g.Player1, "TIME_038");
        for (int used = 0; used < 3; used++)
        {
            var clock = Stopwatch.StartNew(); var bytes = g.SaveSnapshot(); var load = PinnedPoolFixture.LoadSnapshot(bytes); clock.Stop();
            Check(bytes.Length < 8 * 1024 * 1024 && bytes.Length < g.Rewind.Baseline.Length * 4 && bytes.SequenceEqual(load.SaveSnapshot()), "raw baseline blob stays bounded");
            Console.WriteLine($"rewind snapshot: used={used}; baseline={g.Rewind.Baseline.Length}; total={bytes.Length}; save+load={clock.Elapsed.TotalMilliseconds:F1}ms");
            Answer(g, true);
        }
    }
    private static void PublishedOptions()
    {
        var g = Fixture(); var source = Add(g.Player1, "TIME_001");
        var minion = Summon(g.Player1, "CS2_182");
        Generic.AddEnchantmentBlock(g, Cards.FromId("EX1_004e"), g.Player1.Hero, minion);
        int attachment = minion.AppliedEnchantments.Single().Id;
        var options = PowerOptionsBuilder.AllOptions(g, g.Player1.Options());
        Check(options.PlayerTaskList.Count != 0, "published options hold task cache");
        PlayEntity(g, g.Player1, source);
        var bytes = g.SaveSnapshot(); var load = PinnedPoolFixture.LoadSnapshot(bytes);
        Check(bytes.SequenceEqual(load.SaveSnapshot()), "published native option cache survives snapshot");
        Answer(g, true); Answer(load, true);
        Check(Canonical.State(g) == Canonical.State(load) && g.Player1.UsedMana == 3, "rewind after options publication restores/replays");
        Check(g.PowerHistory.Last.OfType<PowerHistoryFullEntity>().Any(e => e.Entity.Id == attachment), "reset snapshots every entity, including attachments");
    }
    private static void Discover()
    {
        var g = Fixture(); Add(g.Player1, "TIME_056");
        var source = Add(g.Player1, "EDR_456"); source[GameTag.REWIND] = 1;
        PlayEntity(g, g.Player1, source);
        Check(g.Rewind?.Stage == RewindStage.Attempt && g.Player1.Choice != null, "Discover nested inside attempt");
        var bytes = g.SaveSnapshot();
        var futures = new List<string>();
        foreach (var game in new[] { g, PinnedPoolFixture.LoadSnapshot(bytes), g.CloneExact(history: true) })
        {
            while (game.Rewind?.Stage == RewindStage.Attempt && game.Player1.Choice != null)
                PlayerDecisionProof.Answer(game, game.Player1.Choice.Choices[0]);
            Check(game.Rewind?.Stage == RewindStage.Offered && game.Player1.HandZone.Any(c => c.Card.Id != "TIME_056"), "Discover and gift settle before offer");
            Answer(game, true);
            while (game.Player1.Choice != null) PlayerDecisionProof.Answer(game, game.Player1.Choice.Choices[0]);
            Check(game.Rewind == null && game.Player1.NumCardsPlayedThisTurn == 1 && game.Player1.UsedMana == 1,
                "Discover replay finishes single net play");
            futures.Add(Canonical.State(game));
        }
        Check(futures.Distinct().Count() == 1, "Discover mid-attempt load/clone future parity");
    }
    private static void TargetsPositionsAndDiscounts()
    {
        var g = Fixture(); var p = g.Player1;
        var target = Summon(p.Opponent, "CS2_186");
        var source = Add(p, "TIME_855"); source[GameTag.REWIND] = 1;
        PlayEntity(g, p, source, target); Answer(g, true);
        Check(g.IdEntityDic[target.Id][GameTag.DAMAGE] == 3 && g.Player2.Hero.Damage == 2 &&
            g.Player1.NumCardsPlayedThisTurn == 1 && g.IdEntityDic[source.Id].CardTarget == target.Id, "replay retains original target");

        g = Fixture(seed: 1); p = g.Player1;
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        source = Add(p, "TIME_038");
        Check(g.Process(new PlayCardTask(p, source, zonePosition: 2)), "positioned Rewind play"); Answer(g, true);
        Check(g.Player1.BoardZone.GetSlot(0).Id == location.Id && g.Player1.BoardZone.GetSlot(1).Id == left.Id &&
            g.IdEntityDic[source.Id].ZonePosition == 2 && g.Player1.BoardZone.GetSlot(5).Id == right.Id, "replay retains physical board position beside location");
        Answer(g, false);

        g = Fixture(seed: 1); p = g.Player1;
        Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_895e"), p.Hero, p);
        source = Add(p, "TIME_038"); g.AuraUpdate(); p.BaseMana = 4;
        Check(source.Cost == 1 && g.Process(new PlayCardTask(p, source)), "Clocksworth plays under Full Moon with less than printed mana");
        for (int used = 1; used <= 3; used++)
        {
            Answer(g, true);
            Check(g.Player1.UsedMana == 1 && g.Player1.NumCardsPlayedThisTurn == 1
                && g.IdEntityDic[source.Id].Card.Id == "TIME_038t" + used,
                "Clocksworth variant reapplies hand cost aura before replay payment");
        }
        Check(g.Rewind == null, "discounted Clocksworth exhausts all three retries");

        g = Fixture(); p = g.Player1;
        var pyro = Summon(p, "NEW1_020");
        CostRules.AddNextCardRule(p, setCost: 1, cardType: CardType.SPELL);
        source = Play(g, p, "TIME_001"); Answer(g, true);
        Check(g.Player1.UsedMana == 1 && g.Player1.NumCardsPlayedThisTurn == 1 && g.CostState.Rules.Count == 0 &&
            g.IdEntityDic[pyro.Id][GameTag.DAMAGE] == 1, "one-use discount and after-cast trigger consumed once net");
    }
    private static void CorruptBaseline()
    {
        void Set(RewindTransaction t, string property, object value) =>
            typeof(RewindTransaction).GetProperty(property).SetValue(t, value);
        var cases = new List<(string, Action<Game>)>();
        // Each envelope string has its own guard (header, fingerprint, MVID, pool identity).
        foreach (int field in new[] { 0, 1, 2, 3, 4 })
        {
            int captured = field;
            cases.Add(("envelope-" + field, g => {
                var bytes = (byte[])g.Rewind.Baseline.Clone();
                using var reader = new BinaryReader(new MemoryStream(bytes));
                for (int i = 0; i < captured; i++) { reader.ReadString(); if (i == 0) reader.ReadInt32(); }
                int offset = (int)reader.BaseStream.Position;
                bytes[offset + 1] ^= 1;
                Set(g.Rewind, "Baseline", bytes);
            }));
        }
        cases.Add(("nested-transaction", g => Set(g.Rewind, "Baseline", g.SaveSnapshot())));
        cases.Add(("source-card", g => Set(g.Rewind, "SourceCardId", "TIME_038t1")));
        cases.Add(("source-owner", g => Set(g.Rewind, "ControllerId", g.Player2.Id)));
        cases.Add(("missing-target", g => Set(g.Rewind, "TargetId", int.MaxValue)));
        cases.Add(("position", g => Set(g.Rewind, "ZonePosition", int.MaxValue)));
        cases.Add(("choose-one", g => Set(g.Rewind, "ChooseOne", int.MaxValue)));
        cases.Add(("used", g => Set(g.Rewind, "Used", 1)));
        foreach (var (name, corrupt) in cases)
        {
            var g = Fixture(); Play(g, g.Player1, "TIME_038"); corrupt(g);
            var bytes = g.SaveSnapshot();
            bool rejected = false;
            try { PinnedPoolFixture.LoadSnapshot(bytes); } catch (System.Runtime.Serialization.SerializationException) { rejected = true; }
            Check(rejected, "corrupt baseline load rejected: " + name);
            rejected = false;
            try { typeof(Game).GetMethod("RestoreSnapshotInPlace", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(Fixture(), new object[] { bytes }); }
            catch (TargetInvocationException e) when (e.InnerException is System.Runtime.Serialization.SerializationException) { rejected = true; }
            Check(rejected, "corrupt baseline in-place load rejected: " + name);
            var history = Canonical.Value(g.PowerHistory.Full) + Canonical.Value(g.PowerHistory.Last);
            var rng = g.Random.CaptureState();
            rejected = false;
            try { rejected = !g.Process(ChooseTask.Pick(g.Player1, g.Rewind.RewindEntityId)); }
            catch (System.Runtime.Serialization.SerializationException) { rejected = true; }
            Check(rejected && bytes.SequenceEqual(g.SaveSnapshot()) && rng.Equals(g.Random.CaptureState()) &&
                history == Canonical.Value(g.PowerHistory.Full) + Canonical.Value(g.PowerHistory.Last),
                "corrupt baseline answer mutation-free: " + name);
        }
        Console.WriteLine("PASS Rewind CorruptBaseline: envelope/fingerprint/MVID/pool, nested transaction, replay metadata; load rejection and mutation-free answer");
    }
    private static void BusyBoundary()
    {
        var g = Fixture();
        Generic.SummonBlock(g, (Minion)Entity.FromCard(g.Player2, Cards.FromId("CS2_231")), -1, null);
        var source = Add(g.Player1, "TIME_001");
        Check(g.SummonedMinions.Count != 0, "fixture has unsettled summon");
        long skipped = RewindRules.SkippedNonQuiescentBoundaries;
        Check(g.Process(PlayCardTask.Any(g.Player1, source)) && g.Rewind == null && g.Player1.Choice == null,
            "busy live boundary plays normally without snapshot or offer");
        Check(g.Player1.UsedMana == 3 && g.Player1.NumCardsPlayedThisTurn == 1 &&
            RewindRules.SkippedNonQuiescentBoundaries == skipped + 1, "busy play pays once and diagnoses skipped offer");
    }
    internal static void Run()
    {
        KarovClocksworthAfterExpiredTax();
        ClocksworthCopiedRecipe();
        CardStateRollback();
        RunRoundTwo();
        CorruptBaseline(); BusyBoundary(); Trees(); InvalidInputs(); Persistence(); FreshProcess(); SimulationAndDormancy(); KeepBoth(); Lethal(); NestedDecision(); Discover(); TargetsPositionsAndDiscounts(); PublishedOptions(); SizeAndTime();
        Console.WriteLine("PASS Rewind: budgets 1/3, Keep trees, net accounting, RNG carry, monotonic IDs, rejection immutability, nested/lethal, persistence/clone/fresh-process, simulation, KeepBoth, dormant");
    }
}
