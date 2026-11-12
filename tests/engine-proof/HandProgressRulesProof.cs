using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class HandProgressRulesProof
{
    private static void Check(bool condition, string message) => Core2026Proof.Check(condition, "HandProgress: " + message);
    internal const string Leans = "Unpinned_HandCastScope Unpinned_HandUpgradeTiming Unpinned_HandFresh Unpinned_HandUpgradeCost Unpinned_HandProjection Unpinned_ReforestationEnd";
    internal static void Run()
    {
        OrdinaryChoices(); SpellBodyProgressProof.Run(); PlotTwistResidence(); CastsAndCopies(); CastBoundaries(); NestedCohorts(); Suspended(); UpgradeCosts(); RetainedBuff(); OwnerEnds(); BoundedResidences(); FreshProcess();
        Console.WriteLine("PASS HandProgressRules: " + Leans);
    }
    private static void OrdinaryChoices()
    {
        foreach (bool combo in new[] { false, true })
        {
            var g = HeraldProof.New(CardClass.SHAMAN); var p = g.Player1;
            if (combo) Play(g, p, "GAME_005");
            var held = Add(p, "JAIL_801"); held[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
            Play(g, p, "EDR_528");
            Check(p.Choice != null && held.Card.Id == "JAIL_801" && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1,
                "Nightmare Fuel ordinary Discover waits for all answers, combo=" + combo);
            var bytes = g.SaveSnapshot();
            Check(!g.Process(EndTurnTask.Any(p)) && bytes.SequenceEqual(g.SaveSnapshot()), "ordinary pending body blocks unrelated actions without mutation");
            var conceded = g.CloneExact(history: true);
            Check(conceded.Process(ConcedeTask.Any(conceded.Player1)) && conceded.SpellCompletion == null && conceded.Player1.Choice == null &&
                conceded.IdEntityDic[held.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "concede cancels ordinary body without progress");
            foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(bytes) })
            {
                int answers = 0;
                while (next.Player1.Choice != null)
                {
                    Check(next.IdEntityDic[held.Id] is Spell, "chained Discover cannot upgrade before its final answer");
                    PlayerDecisionProof.Answer(next, next.Player1.Choice.Choices[0]); answers++;
                }
                Check(answers == 1 && next.IdEntityDic[held.Id].Card.Id == "JAIL_801t" &&
                    next.Player1.NumSpellsPlayedThisGame == (combo ? 2 : 1), $"ordinary completion upgrades and runs cast tail once: combo={combo}, answers={answers}, form={next.IdEntityDic[held.Id].Card.Id}, casts={next.Player1.NumSpellsPlayedThisGame}");
            }
        }
        Console.WriteLine("PASS OrdinarySpellChoices: Nightmare Fuel/Combo Dark Gift counter-1 clone/restore; Unpinned_HandUpgradeTiming");
    }
    private static void PlotTwistResidence()
    {
        var g = HeraldProof.New(CardClass.SHAMAN); var p = g.Player1;
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        var held = Add(p, "JAIL_801"); held[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
        Play(g, p, "DAL_602");
        Check(p.HandZone.Single().Id == held.Id && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3 &&
            held[HandProgressRules.Residence] == 2, "Plot Twist redraw starts a distinct residence and misses its own cast");
        foreach (var next in new[] { g, g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            Play(next, next.Player1, "GAME_005");
            Check(next.IdEntityDic[held.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "new residence counts later casts after clone/load");
        }
        Console.WriteLine("PASS PlotTwistResidence: Unpinned_HandFresh Unpinned_HandCastScope");
    }
    private static void CastsAndCopies()
    {
        var g = HeraldProof.New(CardClass.SHAMAN); var p = g.Player1;
        Play(g, p, "GAME_005");
        var held = Add(p, "JAIL_801");
        Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "earlier casts give no retroactive progress");
        Play(g, p, "GAME_005"); Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "owner completed cast");
        var gameplayCopy = Generic.Copy(p, p.Hero, held, Zone.HAND);
        Check(gameplayCopy[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3 && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "gameplay copy fresh");
        var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
        foreach (var next in new[] { clone, loaded })
        {
            Play(next, next.Player1, "GAME_005"); Play(next, next.Player1, "GAME_005");
            var target = next.IdEntityDic[held.Id];
            Check(target is Minion && target.Card.Id == "JAIL_801t" && target.ZonePosition == 0 &&
                target[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && ((Minion)target).AttackDamage == 3 && ((Minion)target).BaseHealth == 3,
                "same id/slot cross-type upgrade in clone/load");
            Check(next.IdEntityDic[gameplayCopy.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "independent counters");
            Check(held.Card.Id == "JAIL_801" && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "original CLR Spell remains isolated");
        }
        Check(Canonical.State(clone) == Canonical.State(loaded), "clone/snapshot continue identically");
        p.DeckZone.Add(p.HandZone.Remove(held)); Generic.Draw(p, held);
        Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "new hand residence fresh");
        var before = Canonical.State(g); var history = Canonical.History(g); var rng = g.Random.CaptureState();
        Check(ReferenceEquals(HandProgressRules.Upgrade(held), held), "nonzero upgrade rejected");
        HandProgressRules.OnHandEntry(held, false);
        Check(Canonical.State(g) == before && Canonical.History(g) == history && g.Random.CaptureState().Equals(rng), "invalid/rebind no-op");
    }
    private static void CastBoundaries()
    {
        var g = HeraldProof.New(CardClass.SHAMAN); var p = g.Player1;
        var held = Add(p, "JAIL_801");
        var spell = (Spell)Entity.FromCard(p, Cards.FromId("GAME_005"));
        Generic.CastSpell(p, g, spell, null, 0);
        Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "effect cast excluded");
        Play(g, p, "CS2_182"); Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "nonspell excluded");
        End(g); Play(g, p.Opponent, "GAME_005"); End(g);
        Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "enemy spell excluded");
        // Drawn during body: the new residence was absent from capture.
        foreach (var c in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(c));
        var drawn = Entity.FromCard(p, Cards.FromId("JAIL_801"), zone: p.DeckZone);
        Play(g, p, "CS2_023");
        Check(drawn[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3 && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "creator cast cannot count new draw");
        // Capture/reentry and replay completion cannot double-count.
        spell = (Spell)Entity.FromCard(p, Cards.FromId("GAME_005"));
        var candidates = HandProgressRules.CaptureCast(spell);
        p.DeckZone.Add(p.HandZone.Remove(held)); Generic.Draw(p, held);
        HandProgressRules.CompleteCast(p, candidates, HistoryCastKind.Player, HistoryCastOutcome.Completed);
        Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3 && drawn[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "stale residence skipped");
        HandProgressRules.CompleteCast(p, candidates, HistoryCastKind.Player, HistoryCastOutcome.Completed);
        Check(drawn[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "replayed completion no-op");
        held[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
        int id = held.Id; PlayEntity(g, p, held, p.Opponent.Hero);
        Check(g.IdEntityDic[id] is Spell && held.Zone == p.GraveyardZone && p.Opponent.Hero.Damage == 4, "self cast resolves spell; cannot upgrade itself");
        // Counterspell resolves in ordinary OnCast, but completion outcome excludes it.
        g = HeraldProof.New(CardClass.SHAMAN); p = g.Player1; held = Add(p, "JAIL_801");
        End(g); Play(g, p.Opponent, "EX1_287"); End(g);
        Play(g, p, "GAME_005");
        Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "countered cast excluded");
    }
    private static void Suspended()
    {
        foreach (string spellId in new[] { "TIME_770", "FIR_900" })
        {
        var g = HeraldProof.New(CardClass.SHAMAN); var p = g.Player1;
        var held = Add(p, "JAIL_801"); held[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
        Play(g, p, spellId);
        Check(p.Choice != null && held.Card.Id == "JAIL_801" && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "early Completed history is not body completion");
        var decision = g.GetPendingDecisions().Single(); int selected = p.Choice.Choices[0];
        var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
        foreach (var next in new[] { g, clone, loaded })
        {
            PlayerDecisionProof.Answer(next, selected);
            Check(next.IdEntityDic[held.Id] is Minion && next.IdEntityDic[held.Id].Card.Id == "JAIL_801t", "final decision cast-tail upgrades once");
            var bytes = next.SaveSnapshot();
            Check(!next.TryAnswer(new DecisionAnswer(decision.Id, p.Id, new[] { selected }), out _) && bytes.SequenceEqual(next.SaveSnapshot()), "double resume rejected without mutations");
        }
        Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(loaded), "suspended cohort clone/load: " + spellId);
        Console.WriteLine("PASS SuspendedSpellBody: " + spellId);
        }
    }
    private static void NestedCohorts()
    {
        var g = HeraldProof.New(CardClass.SHAMAN); var p = g.Player1;
        var held = Add(p, "JAIL_801");
        var outer = HandProgressRules.CaptureCast((Spell)Entity.FromCard(p, Cards.FromId("GAME_005")));
        var inner = HandProgressRules.CaptureCast((Spell)Entity.FromCard(p, Cards.FromId("GAME_005")));
        HandProgressRules.CompleteCast(p, inner, HistoryCastKind.Player, HistoryCastOutcome.Completed);
        HandProgressRules.CompleteCast(p, outer, HistoryCastKind.Player, HistoryCastOutcome.Completed);
        Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "nested completion cannot erase outer cast cohort");
        HandProgressRules.CompleteCast(p, inner, HistoryCastKind.Player, HistoryCastOutcome.Completed);
        HandProgressRules.CompleteCast(p, outer, HistoryCastKind.Player, HistoryCastOutcome.Completed);
        Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "each nested cohort consumed once");
    }
    private static void UpgradeCosts()
    {
        var g = HeraldProof.New(CardClass.SHAMAN); var p = g.Player1;
        var held = (Playable)Add(p, "JAIL_801");
        Effects.ReduceCost(2).ApplyTo(held); CostRules.SetCostThisTurn(held, 0);
        CostRules.AddPaymentOverride(held, CostPaymentKind.Health, g.Turn);
        held[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
        Play(g, p, "GAME_005");
        var target = g.IdEntityDic[held.Id];
        Check(target.Cost == 3 && CostRules.PaymentKind(target) == CostPaymentKind.Mana && target is Minion,
            "drop permanent/SET/alternate costs; target definition rebased");
        Check(target[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && target[GameTag.SPELL_SCHOOL] == 0 && target[GameTag.BATTLECRY] == 1, "old counter/school clear; target powers bound");
        PlayEntity(g, p, target, p.Opponent.Hero);
        Check(p.Opponent.Hero.Damage == 4, "upgraded Battlecry bound");
        Generic.ReturnToHandBlock(p, (Minion)target);
        Check(target.Card.Id == "JAIL_801t" && target.Zone == p.HandZone, "bounce retains current target definition");
        PlayEntity(g, p, target, p.Opponent.Hero); ((Minion)target).Silence(); g.DeathProcessingAndAuraUpdate();
        Check(((Minion)target).IsSilenced && target[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "ordinary Silence leaves no countdown");
    }
    private static void OwnerEnds()
    {
        // Exercise OwnerTurnEnds API without admitting another collectible in this slice.
        var definition = Cards.FromId("JAIL_801"); var original = definition.HandProgress;
        try
        {
            definition.HandProgress = new HandProgressDefinition("JAIL_801t", 3, HandProgressMode.OwnerTurnEnds);
            var g = HeraldProof.New(CardClass.SHAMAN); var p = g.Player1; var held = Add(p, "JAIL_801");
            End(g); Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "receiving own turn counts");
            End(g); Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "enemy end excluded");
            End(g); Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "second owner end");
            End(g); End(g);
            Check(g.IdEntityDic[held.Id].Card.Id == "JAIL_801t", "third owner end upgrades");
        }
        finally { definition.HandProgress = original; }
    }
    private static void RetainedBuff()
    {
        foreach (bool history in new[] { false, true })
        {
        var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
        var held = Add(p, "JAIL_801");
        Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_087e"), p.Hero, held);
        held[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
        Play(g, p, "GAME_005");
        var target = (Minion)g.IdEntityDic[held.Id];
        Check(target.AttackDamage == 6 && target.BaseHealth == 3 &&
            target.AppliedEnchantments.Single().Target == target, "compatible +3 Attack retained and rebound across CLR replacement");
        if (history)
        {
            var change = g.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistoryChangeEntity>().Last(e => e.Entity.Id == target.Id);
            Check(change.Entity.Tags[GameTag.ATK] == 6, "CHANGE publishes retained Attack rather than printed base");
        }
        PlayEntity(g, p, target, p.Opponent.Hero); target.Silence(); g.DeathProcessingAndAuraUpdate();
        Check(target.AttackDamage == 3, "retained enchantment obeys ordinary Silence");
        }
    }
    private static string SnapshotFuture(Game game)
    {
        PlayerDecisionProof.Answer(game, game.Player1.Choice.Choices.First());
        Check(game.Player1.HandZone.Any(c => c.Card.Id == "JAIL_801t"), "restored suspended cohort upgrades");
        foreach (var card in game.Player1.HandZone.Where(c => c.Card.Shatter == null && c.Card.Id != "JAIL_801t").ToArray())
            game.Player1.SetasideZone.Add(game.Player1.HandZone.Remove(card));
        Check(game.Player1.HandZone.Any(c => c.Card.Id == "CATA_489" && c[ShatterRules.AlreadyCombined] == 1), "restored halves join with permanent marker");
        return Canonical.State(game) + Canonical.History(game) + Canonical.Value(game.Random.CaptureState());
    }
    private static void BoundedResidences()
    {
        var game = HeraldProof.New(CardClass.SHAMAN); var p = game.Player1;
        var held = Add(p, "JAIL_801");
        var rng = game.Random.CaptureState();
        for (int i = 0; i < 10000; i++)
        {
            p.SetasideZone.Add(p.HandZone.Remove(held));
            p.HandZone.Add(p.SetasideZone.Remove(held));
        }
        Check(held[HandProgressRules.Residence] == 10001 && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3,
            "10000 prior residences never contribute retroactive progress");
        var cohort = HandProgressRules.CaptureCast((Spell)Entity.FromCard(p, Cards.FromId("GAME_005")));
        Check(cohort.Length == 1, "cohort bounded by current hand after 10000 residences");
        HandProgressRules.CompleteCast(p, cohort, HistoryCastKind.Player, HistoryCastOutcome.Completed);
        Add(p, "CATA_489");
        Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && p.HandZone.Count == 3 &&
            p.HandZone.Count(c => c.Card.Shatter != null) == 2 && game.Random.CaptureState().Equals(rng),
            "Shatter and progress stay local after 10000 residences, without RNG");
        Console.WriteLine("PASS card-state bounded work: 10000 prior residences, current-hand cohorts and joins only");
    }
    internal static void Child(string directory)
    {
        var bytes = File.ReadAllBytes(Path.Combine(directory, "state.snapshot"));
        var game = Game.LoadSnapshot(bytes);
        Check(bytes.SequenceEqual(game.SaveSnapshot()), "fresh-process byte round trip");
        Check(SnapshotFuture(game) == File.ReadAllText(Path.Combine(directory, "future.txt")), "fresh-process continuation identical");
        Console.WriteLine("PASS card-state fresh-process: Shatter halves/modifiers and suspended hand-progress cohort");
    }
    private static void FreshProcess()
    {
        var game = HeraldProof.New(CardClass.SHAMAN); var p = game.Player1;
        Add(p, "CS2_182");
        var parent = Entity.FromCard(p, Cards.FromId("CATA_489"));
        Effects.ReduceCost(1).ApplyTo((Playable)parent); Generic.AddHandPhase(p, parent);
        Add(p, "JAIL_801")[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
        Play(game, p, "TIME_770");
        string directory = Path.Combine(Path.GetTempPath(), "hs-card-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var bytes = game.SaveSnapshot();
            File.WriteAllBytes(Path.Combine(directory, "state.snapshot"), bytes);
            File.WriteAllText(Path.Combine(directory, "future.txt"), SnapshotFuture(Game.LoadSnapshot(bytes)));
            var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath) { RedirectStandardOutput = true, RedirectStandardError = true };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet")
                start.ArgumentList.Add(System.Reflection.Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--card-state-child"); start.ArgumentList.Add(directory);
            using var child = System.Diagnostics.Process.Start(start);
            string output = child.StandardOutput.ReadToEnd(), error = child.StandardError.ReadToEnd();
            Check(child.WaitForExit(30000) && child.ExitCode == 0, "fresh process: " + output + error);
            Console.Write(output);
        }
        finally { Directory.Delete(directory, true); }
    }
}
