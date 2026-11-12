using HsSim;
using SabberStoneCore.Enchants;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static class PlayerDecisionProof
{
    // Test-only continuation exercises legacy insertion, not retail shuffle semantics.
    private static readonly DecisionBattlecryTask HandRecipe = new DecisionBattlecryTask("proof/decision/hand/v1",
        new SelectEntityThenContinueTask(),
        EffectContext.Task(ctx => {
            if (ctx.LastChosenEntityId == 0) return TaskState.COMPLETE;
            ctx.RunLegacy(new MoveToDeck(EntityType.TARGET), ctx.LastChosenEntityId);
            return ctx.RunLegacy(new CustomTask((g, c, s, t, stack) =>
                new Enchant(Effects.ReduceCost(1)).ActivateTo(t)), ctx.LastChosenEntityId);
        }));

    private static void WithHandRecipe(Action proof)
    {
        var card = Cards.FromId("EX1_319"); var original = card.Power;
        try { card.Power = new Power { PowerTask = HandRecipe }; proof(); }
        finally { card.Power = original; }
    }

    internal static void Check(bool value, string message)
    { if (!value) throw new Exception("Player decision: " + message); }

    internal static Game Fixture(int seed = 1, bool brann = false)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, SkipMulligan = true,
            History = true, Logging = false, Player1HeroClass = CardClass.ROGUE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        foreach (var card in game.Player1.HandZone.ToArray()) { game.Player1.HandZone.Remove(card); game.Player1.SetasideZone.Add(card); }
        game.Player1.BaseMana = 10;
        if (brann) Core2026Proof.Summon(game.Player1, "LOE_077");
        return game;
    }

    internal static void Answer(Game game, int entityId)
    {
        var decision = game.GetPendingDecisions().Single();
        Check(game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { entityId }), out var error), "answer " + error);
    }

    private static void Reject(Game game, DecisionAnswer answer)
    {
        var before = game.SaveSnapshot();
        Check(!game.TryAnswer(answer, out _), "reject malformed/stale answer");
        Check(before.SequenceEqual(game.SaveSnapshot()), "rejection must not mutate state/history/RNG");
    }

    private static void Hand(int seed, int count, bool brann)
    {
        var game = Fixture(seed, brann); var p = game.Player1;
        var cards = Enumerable.Range(0, count).Select(_ => Generic.DrawCard(p, Cards.FromId("CS2_182"))).ToArray();
        var source = Generic.DrawCard(p, Cards.FromId("EX1_319"));
        var rng = game.Random.CaptureState();
        Check(game.Process(PlayCardTask.Any(p, source)), "legal play");
        Check(p.UsedMana == 1 && p.PlayHistory.Count == 1 && source.Zone.Type == Zone.PLAY, "pay, record, summon once");
        Check(game.Random.CaptureState().Equals(rng), "opening selection draws no RNG");
        int rounds = Math.Min(count, brann ? 2 : 1);
        for (int i = 0; i < rounds; i++)
        {
            var prompt = game.GetPendingDecisions().Single();
            Check(prompt.Kind == DecisionKind.SuspendedEffect && p.Choice.ChoiceType == ChoiceType.TARGET &&
                p.Choice.SourceId == source.Id && prompt.MinSelections == 1 && prompt.MaxSelections == 1, "TARGET identity and cardinality");
            Check(p.Choice.Choices.SequenceEqual(cards.Skip(i).Select(c => c.Id)), "current hand in order, duplicate identities distinct, source excluded");
            Check(p.Options().All(t => t is ChooseTask) && p.Options().Count == count-i, "bot legal selections");
            Check(!p.IsComboActive, "outer play completion deferred");
            var pick = cards[i].Id;
            Reject(game, new DecisionAnswer(prompt.Id+1, p.Id, new[] { pick }));
            Reject(game, new DecisionAnswer(prompt.Id, p.Opponent.Id, new[] { pick }));
            Reject(game, new DecisionAnswer(prompt.Id, p.Id, new[] { source.Id }));
            Reject(game, new DecisionAnswer(prompt.Id, p.Id, new[] { pick, pick }));
            var bytes = game.SaveSnapshot();
            var restored = Game.LoadSnapshot(bytes); var clone = game.CloneExact(history: true);
            Check(bytes.SequenceEqual(restored.SaveSnapshot()), "stable suspended snapshot");
            foreach (var candidate in new[] { game, restored, clone }) Answer(candidate, pick);
            Check(Canonical.State(game) == Canonical.State(clone) && Canonical.State(game) == Canonical.State(restored), "clone/restored future parity");
            Check(Canonical.History(game) == Canonical.History(restored), "restored history parity");
            Check(game.Random.CaptureState().Equals(clone.Random.CaptureState()) && game.Random.CaptureState().Equals(restored.Random.CaptureState()), "shuffle RNG parity");
            Check(cards[i].Zone.Type == Zone.DECK && cards[i].Cost == 3, "shuffle exact selected entity then discount");
            Reject(game, new DecisionAnswer(prompt.Id, p.Id, new[] { pick }));
        }
        Check(!game.Resolution.HasSuspendedEffect && p.Choice == null && p.IsComboActive, "empty/last answer completes play");
        Check(p.PlayHistory.Count == 1 && p.UsedMana == 1 && p.NumMinionsPlayedThisTurn == 1, "no duplicated payment/play/summon");
        Check(game.Resolution.Depth == 0 && game.Resolution.FramesStarted == game.Resolution.FramesEnded, "balanced resolution");
        game.SaveSnapshot();
    }

    private static void ShuffleReactionsBeforeRepeat()
    {
        var game = Fixture(23, true); var p = game.Player1;
        Core2026Proof.Summon(p, "DAL_719");
        var selected = Generic.DrawCard(p, Cards.FromId("CS2_182"));
        Core2026Proof.Play(game, p, "EX1_319");
        Answer(game, selected.Id);
        Check(p.Choice != null && p.HandZone.Count == 1 &&
            p.Choice.Choices.SequenceEqual(p.HandZone.Select(c => c.Id)),
            "Tak copy must arrive before Brann rebuilds the second hand offer");
        var copy = p.HandZone.Single();
        Check(copy.Id != selected.Id && copy.Card.Id == selected.Card.Id, "Tak offers a distinct copied entity");
        var restored = Game.LoadSnapshot(game.SaveSnapshot()); var clone = game.CloneExact(history: true);
        foreach (var candidate in new[] { game, restored, clone }) Answer(candidate, copy.Id);
        Check(selected.Zone.Type == Zone.DECK && copy.Zone.Type == Zone.DECK && p.HandZone.Count == 1 && p.Choice == null,
            "both shuffles and both Tak reactions resolve exactly once");
        Check(Canonical.State(game) == Canonical.State(restored) && Canonical.State(game) == Canonical.State(clone),
            "Tak repetition clone/snapshot future");
    }

    private static readonly DecisionBattlecryTask LethalRecipe = new DecisionBattlecryTask("proof/decision/lethal/v1",
        new DiscoverThenContinueTask("UNG_999t6", "UNG_999t13"),
        EffectContext.Task(ctx => ctx.RunLegacy(new CustomTask((g, c, s, t, stack) => {
            // Damage an aura source and a Deathrattle minion during each activation.
            foreach (var victim in c.BoardZone.Where(m => m.Card.Id == "CS2_122" || m.Card.Id == "EX1_096").ToArray())
                victim.TakeDamage((IPlayable)s, 10);
        }))));

    private static void DeathsWaitForBattlecryCompletion()
    {
        var card = Cards.FromId("EX1_319"); var original = card.Power;
        try
        {
            card.Power = new Power { PowerTask = LethalRecipe };
            var game = Fixture(29, true); var p = game.Player1;
            var leader = Core2026Proof.Summon(p, "CS2_122");
            var hoarder = Core2026Proof.Summon(p, "EX1_096");
            var source = (Minion)Core2026Proof.Play(game, p, "EX1_319");
            Answer(game, p.Choice.Choices[0]);
            Check(p.Choice != null && leader.Zone.Type == Zone.PLAY && hoarder.Zone.Type == Zone.PLAY &&
                leader.Health <= 0 && hoarder.Health <= 0 && p.HandZone.Count == 0 && source.AttackDamage == 4,
                "lethal damage must not resolve deaths, Deathrattle draw, or aura removal between Brann prompts");
            var bytes = game.SaveSnapshot();
            var restored = Game.LoadSnapshot(bytes); var clone = game.CloneExact(history: true);
            Check(bytes.SequenceEqual(restored.SaveSnapshot()), "pending deaths snapshot bytes");
            foreach (var candidate in new[] { game, restored, clone })
            {
                Answer(candidate, candidate.Player1.Choice.Choices[0]);
                Check(candidate.Player1.Choice == null && candidate.Player1.HandZone.Count == 1 &&
                    candidate.IdEntityDic[leader.Id].Zone.Type == Zone.GRAVEYARD &&
                    candidate.IdEntityDic[hoarder.Id].Zone.Type == Zone.GRAVEYARD &&
                    ((Minion)candidate.IdEntityDic[source.Id]).AttackDamage == 3,
                    "final activation settles deaths, one Deathrattle draw, and aura removal");
            }
            Check(Canonical.State(game) == Canonical.State(restored) && Canonical.State(game) == Canonical.State(clone),
                "pending death clone/snapshot future");
        }
        finally { card.Power = original; }
    }

    private static void StaleAndConcede()
    {
        var game = Fixture(); var p = game.Player1;
        var chosen = Generic.DrawCard(p, Cards.FromId("CS2_182"));
        Core2026Proof.Play(game, p, "EX1_319");
        var view = game.GetPendingDecisions().Single();
        p.HandZone.Remove(chosen); p.SetasideZone.Add(chosen);
        Reject(game, new DecisionAnswer(view.Id, p.Id, new[] { chosen.Id }));
        Check(game.Process(ConcedeTask.Any(p)), "concede at selection");
        Check(!game.Resolution.HasSuspendedEffect && !Generic.HasDecisionPlay(p) && p.Choice == null, "concede drops all continuations");
    }

    private static void SourceRemoved()
    {
        var game = Fixture(); var p = game.Player1;
        var selected = Generic.DrawCard(p, Cards.FromId("CS2_182"));
        var source = (Minion)Core2026Proof.Play(game, p, "EX1_319");
        Core2026Proof.Kill(game, source);
        var restored = Game.LoadSnapshot(game.SaveSnapshot());
        Answer(game, selected.Id); Answer(restored, selected.Id);
        Check(source.Zone.Type == Zone.GRAVEYARD && selected.Zone.Type == Zone.DECK && selected.Cost == 3,
            "committed hand manipulation finishes after source removal");
        Check(Canonical.State(game) == Canonical.State(restored), "removed source snapshot future");
    }

    private static readonly DecisionBattlecryTask FixedRecipe = new DecisionBattlecryTask("proof/decision/fixed/v1",
        new DiscoverThenContinueTask("UNG_999t6", "UNG_999t13", "UNG_999t14"),
        EffectContext.Task(ctx => ctx.GiveBuff(ctx.SourceId, ctx.LastChosenCardId + "e")));

    private static void Fixed()
    {
        // Fixture recipe tests fixed-option mechanics; it does not assert a retail parent/token association.
        var card = Cards.FromId("EX1_319"); var original = card.Power;
        try
        {
            card.Power = new Power { PowerTask = FixedRecipe };
            for (int branch = 0; branch < 3; branch++)
            {
                var game = Fixture(31, true); var p = game.Player1;
                var source = (Minion)Core2026Proof.Play(game, p, "EX1_319");
                var rng = game.Random.CaptureState();
                for (int i = 0; i < 2; i++)
                {
                    Check(p.Choice.ChoiceType == ChoiceType.GENERAL && p.Choice.Choices.Count == 3, "fixed GENERAL offers");
                    var copy = Game.LoadSnapshot(game.SaveSnapshot());
                    int pick = p.Choice.Choices[branch];
                    Answer(game, pick); Answer(copy, pick);
                    Check(Canonical.State(game) == Canonical.State(copy), "fixed choice saved continuation");
                }
                Check(source.HasTaunt == (branch == 0) && source.Poisonous == (branch == 1) &&
                    source.AttackDamage == (branch == 2 ? 5 : 3) && source.Health == (branch == 2 ? 4 : 2), "selected branch only, repeats sequentially");
                Check(game.Random.CaptureState().Equals(rng), "fixed branches consume no RNG");
                source.Silence();
                Check(!source.HasTaunt && !source.Poisonous && source.AttackDamage == 3 && source.Health == 2, "Silence removes gained effects");
            }
        }
        finally { card.Power = original; }
    }

    private static void Replay()
    {
        var game = Fixture(17); var p = game.Player1;
        Generic.DrawCard(p, Cards.FromId("CS2_182"));
        Core2026Proof.Play(game, p, "EX1_319"); Answer(game, p.Choice.Choices[0]);
        var first = Generic.DrawCard(p, Cards.FromId("CS2_182"));
        var second = Generic.DrawCard(p, Cards.FromId("CS2_182"));
        Core2026Proof.Play(game, p, "GIL_820");
        Check(p.Choice == null && !game.Resolution.HasSuspendedEffect && p.HandZone.Count == 1, "Shudderwock answers hand decision automatically");
        Check(new[] { first, second }.Single(c => c.Zone.Type == Zone.DECK).Cost == 3, "replay selected current hand and applied discount");
    }

    private static void AfterPlayAndEcho()
    {
        var game = Fixture(3, true); var p = game.Player1;
        Core2026Proof.Summon(p, "GIL_618");
        Core2026Proof.End(game); Core2026Proof.Play(game, p.Opponent, "EX1_294"); Core2026Proof.End(game);
        Generic.DrawCard(p, Cards.FromId("CS2_182"));
        var source = Generic.DrawCard(p, Cards.FromId("EX1_319")); game.AuraUpdate(); Check(source.IsEcho, "Glinda supplies Echo");
        Check(game.Process(PlayCardTask.Any(p, source)), "Echo decision play");
        Check(p.Opponent.BoardZone.Count == 0 && p.HandZone.All(c => c.Card.Id != "EX1_319"), "Mirror Entity and Echo deferred");
        var restored = Game.LoadSnapshot(game.SaveSnapshot());
        Answer(restored, restored.Player1.Choice.Choices[0]);
        Answer(game, p.Choice.Choices[0]);
        Check(Canonical.State(game) == Canonical.State(restored), "deferred secret clone/snapshot parity");
        Check(p.Opponent.BoardZone.Count == 0 && p.HandZone.All(c => c.Card.Id != "EX1_319"), "still deferred between Brann activations");
        Answer(game, p.Choice.Choices[0]);
        Check(p.Opponent.BoardZone.Single().Card.Id == "EX1_319" && p.HandZone.Single().Card.Id == "EX1_319" &&
            p.HandZone.Single()[GameTag.GHOSTLY] == 1, "after-play secret and Echo execute once after final answer");
        int depth = 0;
        foreach (var entry in game.PowerHistory.Full)
        {
            if (entry is SabberStoneCore.Kettle.PowerHistoryBlockStart) depth++;
            if (entry is SabberStoneCore.Kettle.PowerHistoryBlockEnd) depth--;
            Check(depth >= 0, "history has no unmatched ends");
        }
        Check(depth == 0, "history closes at answer boundary");
    }

    private static string Future(Game game)
    {
        while (game.Resolution.HasSuspendedEffect) Answer(game, game.Player1.Choice.Choices[0]);
        return Canonical.Sha(Canonical.State(game) + Canonical.History(game) + Canonical.Value(game.Random.CaptureState()));
    }

    internal static void Child(string file) => WithHandRecipe(() => ChildCore(file));

    private static void ChildCore(string file)
    {
        var bytes = File.ReadAllBytes(file); var game = Game.LoadSnapshot(bytes);
        Check(bytes.SequenceEqual(game.SaveSnapshot()), "fresh-process snapshot bytes");
        Check(Future(game) == File.ReadAllText(file + ".hash"), "fresh-process future");
        Console.WriteLine("PASS player decisions fresh-process restore");
    }

    private static void FreshProcess()
    {
        string file = Path.Combine(Path.GetTempPath(), "hs-decision-" + Guid.NewGuid().ToString("N"));
        var game = Fixture(19, true);
        Generic.DrawCard(game.Player1, Cards.FromId("CS2_182")); Generic.DrawCard(game.Player1, Cards.FromId("CS2_182"));
        Core2026Proof.Play(game, game.Player1, "EX1_319");
        try
        {
            for (int stage = 0; stage < 2; stage++)
            {
                var bytes = game.SaveSnapshot(); File.WriteAllBytes(file, bytes);
                File.WriteAllText(file + ".hash", Future(Game.LoadSnapshot(bytes)));
                var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath) { RedirectStandardOutput = true, RedirectStandardError = true };
                if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(System.Reflection.Assembly.GetExecutingAssembly().Location);
                start.ArgumentList.Add("--player-decision-child"); start.ArgumentList.Add(file);
                using var child = System.Diagnostics.Process.Start(start);
                string output = child.StandardOutput.ReadToEnd(), error = child.StandardError.ReadToEnd();
                Check(child.WaitForExit(30000) && child.ExitCode == 0, "child: " + output + error);
                Answer(game, game.Player1.Choice.Choices[0]);
            }
        }
        finally { File.Delete(file); File.Delete(file + ".hash"); }
    }

    private static readonly SelectorSpec FelHand = new SelectorSpec(SelectorSource.Hand,
        cardType: CardType.SPELL, spellSchool: 7, maximumCurrentCost: 3, groupKey: "fel", excludeEarlierPicks: true);
    private static readonly DecisionBattlecryTask RepeatRecipe = new DecisionBattlecryTask("proof/selector/repeat/v1",
        StateTaskList.SelectRepeated("proof/selector/repeated/v1", FelHand, 3,
            EffectContext.Task(ctx => ctx.LastChosenEntityId == 0 ? TaskState.COMPLETE :
                ctx.RunLegacy(new TransformTask("CS2_182", EntityType.TARGET), ctx.LastChosenEntityId))));
    private static readonly DecisionBattlecryTask BoardRecipe = new DecisionBattlecryTask("proof/selector/board/v1",
        StateTaskList.SelectRepeated("proof/selector/board-picks/v1",
            new SelectorSpec(SelectorSource.Board, SelectorSide.Enemy, includeHeroes: true,
                groupKey: "board", excludeEarlierPicks: true, excludePlayTarget: true), 3,
            EffectContext.Task(ctx => TaskState.COMPLETE)));
    private static readonly DecisionBattlecryTask StopRecipe = new DecisionBattlecryTask("proof/selector/stop/v1",
        StateTaskList.SelectRepeated("proof/selector/stop-group/v1",
            new SelectorSpec(SelectorSource.Hand, cardType: CardType.SPELL, spellSchool: 7,
                groupKey: "stop", excludeEarlierPicks: true, ifEmpty: EmptySelectorAction.StopRecipe), 3,
            EffectContext.Task(ctx => ctx.RunLegacy(new CustomTask((g, c, source, t, stack) => c.Hero.Armor++)))),
        EffectContext.Task(ctx => ctx.RunLegacy(new CustomTask((g, c, source, t, stack) => c.Hero.Armor += 10))));
    private static readonly DecisionBattlecryTask ContinueRecipe = new DecisionBattlecryTask("proof/selector/continue/v1",
        StateTaskList.SelectRepeated("proof/selector/continue-group/v1",
            new SelectorSpec(SelectorSource.Hand, cardType: CardType.SPELL, spellSchool: 7,
                groupKey: "continue", excludeEarlierPicks: true), 3,
            EffectContext.Task(ctx => ctx.RunLegacy(new CustomTask((g, c, source, t, stack) =>
                c.Hero.Armor += ctx.LastChosenEntityId == 0 ? 10 : 1)))));

    private static void WithRecipe(DecisionBattlecryTask recipe, Action proof)
    {
        var card = Cards.FromId("EX1_319"); var power = card.Power;
        try { card.Power = new Power { PowerTask = recipe }; proof(); }
        finally { card.Power = power; }
    }

    private static void FilteredHand()
    {
        WithRecipe(RepeatRecipe, () => {
            var game = Fixture(); var p = game.Player1;
            var first = Generic.DrawCard(p, Cards.FromId("CORE_BT_801"));
            var second = Generic.DrawCard(p, Cards.FromId("CORE_BT_801"));
            var costly = Generic.DrawCard(p, Cards.FromId("CORE_BT_801")); costly.Cost = 8;
            Generic.DrawCard(p, Cards.FromId("CS2_029")); Generic.DrawCard(p, Cards.FromId("CS2_182"));
            Core2026Proof.Play(game, p, "EX1_319");
            Check(p.Choice.Choices.SequenceEqual(new[] { first.Id, second.Id }), "FilteredHand: type/school/effective cost and physical duplicates");
            Answer(game, first.Id);
            Check(p.Choice.Choices.SequenceEqual(new[] { second.Id }), "RepeatReevaluates: transformed pick no longer Fel, singleton prompts");
            new Enchant(Effects.ReduceCost(6)).ActivateTo(costly);
            Answer(game, second.Id);
            Check(p.Choice.Choices.SequenceEqual(new[] { costly.Id }), "CandidateChangesBetweenSteps: current cost re-evaluated, new candidate admitted");
            SnapshotProof.CorruptSelectorGroupHistoryRejected(game);
            Answer(game, costly.Id);
            Check(p.Choice == null && new[] { first, second, costly }.All(c => game.IdEntityDic[c.Id].Card.Id == "CS2_182"), "three continuations complete");
        });
    }

    private static void CandidateChangesBetweenSteps()
    {
        WithRecipe(RepeatRecipe, () => {
            var game = Fixture(); var p = game.Player1;
            var first = Generic.DrawCard(p, Cards.FromId("CORE_BT_801"));
            var gone = Generic.DrawCard(p, Cards.FromId("CORE_BT_801"));
            var transformed = Generic.DrawCard(p, Cards.FromId("CORE_BT_801"));
            Core2026Proof.Play(game, p, "EX1_319");
            p.HandZone.Remove(gone); p.SetasideZone.Add(gone);
            new TransformTask("CS2_029", EntityType.TARGET).Process(game, p, p.Hero, transformed);
            var replacement = Generic.DrawCard(p, Cards.FromId("CORE_BT_801"));
            Answer(game, first.Id);
            Check(p.Choice.Choices.SequenceEqual(new[] { replacement.Id }), "CandidateChangesBetweenSteps: moved/transformed cards excluded, new physical entity admitted");
            Answer(game, replacement.Id);
            Check(p.Choice == null, "empty later repeat completes without prompt");
        });
    }

    private static void ExcludePhysicalEntity()
    {
        WithRecipe(StopRecipe, () => {
            var game = Fixture(); var p = game.Player1;
            var first = Generic.DrawCard(p, Cards.FromId("CORE_BT_801"));
            var second = Generic.DrawCard(p, Cards.FromId("CORE_BT_801"));
            Core2026Proof.Play(game, p, "EX1_319"); Answer(game, first.Id);
            Check(p.Choice.Choices.SequenceEqual(new[] { second.Id }), "ExcludePhysicalEntity: same definition remains as distinct candidate");
            var restored = Game.LoadSnapshot(game.SaveSnapshot());
            SnapshotProof.CorruptSelectorGroupHistoryRejected(game);
            var clone = game.CloneExact(history: true);
            Check(!ReferenceEquals(game.Resolution.SuspendedEffect.SelectedInGroup, clone.Resolution.SuspendedEffect.SelectedInGroup), "nonempty group picks deep cloned");
            Answer(clone, second.Id);
            Check(p.Choice != null && p.Hero.Armor == 1, "group clone continuation isolated");
            Answer(game, second.Id); Answer(restored, second.Id);
            Check(p.Hero.Armor == 12 && p.Choice == null && Canonical.State(game) == Canonical.State(restored),
                "EmptySelectorStopsGroup: skip remaining nested picks, continue outer work");
        });
    }

    private static void EmptySelectorContinues()
    {
        foreach (bool hasCandidate in new[] { false, true }) WithRecipe(ContinueRecipe, () => {
            var game = Fixture(); var p = game.Player1;
            var selected = hasCandidate ? Generic.DrawCard(p, Cards.FromId("CORE_BT_801")) : null;
            Core2026Proof.Play(game, p, "EX1_319");
            if (hasCandidate) { Check(p.Choice.Choices.Count == 1, "SingletonStillPrompts"); Answer(game, selected.Id); }
            Check(p.Choice == null && p.Hero.Armor == (hasCandidate ? 21 : 30), "EmptySelectorContinues: no empty choice, no stale last pick");
        });
        WithRecipe(StopRecipe, () => {
            var game = Fixture(); Core2026Proof.Play(game, game.Player1, "EX1_319");
            Check(game.Player1.Hero.Armor == 10, "empty StopRecipe leaves outer tail");
        });
    }

    private static void BoardSelectors()
    {
        WithRecipe(BoardRecipe, () => {
            var game = Fixture(); var p = game.Player1;
            var enemy = Core2026Proof.Summon(p.Opponent, "CS2_182");
            var elusive = Core2026Proof.Summon(p.Opponent, "CS2_182"); elusive.CantBeTargetedBySpells = true;
            var stealth = Core2026Proof.Summon(p.Opponent, "CS2_182"); stealth.HasStealth = true;
            var location = Entity.FromCard(p.Opponent, Cards.FromId("JAIL_877"), zone: p.Opponent.BoardZone);
            var source = Core2026Proof.Play(game, p, "EX1_319");
            Check(p.Choice.Choices.SequenceEqual(new[] { p.Opponent.Hero.Id, enemy.Id, elusive.Id }.OrderBy(id => id)),
                "board side/hero/minion policy, Elusive allowed for Battlecry; no locations/stealth");
            Answer(game, enemy.Id);
            Check(!p.Choice.Choices.Contains(enemy.Id) && p.Choice.Choices.Contains(elusive.Id), "earlier physical board pick excluded");
            Answer(game, elusive.Id); Answer(game, p.Opponent.Hero.Id);
            Check(p.Choice == null, "board repeats complete");
        });
        // Direct descriptor fixture supplies an effective first target without registering another card.
        var g = Fixture(); var p1 = g.Player1;
        var a = Core2026Proof.Summon(p1.Opponent, "CS2_182"); var b = Core2026Proof.Summon(p1.Opponent, "CS2_182");
        BoardRecipe.Recipe.Process(g, p1, p1.Hero, a);
        Check(!p1.Choice.Choices.Contains(a.Id) && p1.Choice.Choices.Contains(b.Id), "ExcludePlayTarget uses effective physical first target");
        while (p1.Choice != null) Answer(g, p1.Choice.Choices[0]);
    }

    private static void SourceDiesTransformsBounces()
    {
        foreach (string cardId in new[] { "CATA_697", "BE_036" })
        foreach (string action in new[] { "dies", "transforms", "bounces" })
        {
            var game = Fixture(); var p = game.Player1;
            var chosen = Generic.DrawCard(p, Cards.FromId(cardId == "CATA_697" ? "CORE_BT_801" : "CS2_182"));
            var source = (Minion)Core2026Proof.Play(game, p, cardId);
            if (action == "dies") Core2026Proof.Kill(game, source);
            if (action == "transforms") new TransformTask("CS2_182", EntityType.SOURCE).Process(game, p, source, null);
            if (action == "bounces") new ReturnHandTask(EntityType.SOURCE).Process(game, p, source, null);
            var restored = Game.LoadSnapshot(game.SaveSnapshot());
            Answer(game, chosen.Id); Answer(restored, chosen.Id);
            Check((cardId == "CATA_697" ? p.HandZone.Count(c => c.Card.Id == chosen.Card.Id) == 2 : chosen.Cost == 0) && p.Choice == null &&
                Canonical.State(game) == Canonical.State(restored), "committed definition survives source " + cardId + " " + action);
        }
    }

    private static void ConcedeAtEveryStep()
    {
        foreach (bool opponent in new[] { false, true })
        for (int stage = 0; stage < 3; stage++) WithRecipe(StopRecipe, () => {
            var game = Fixture(); var p = game.Player1;
            for (int i = 0; i < 3; i++) Generic.DrawCard(p, Cards.FromId("CORE_BT_801"));
            Core2026Proof.Play(game, p, "EX1_319");
            for (int i = 0; i < stage; i++) Answer(game, p.Choice.Choices[0]);
            var rng = game.Random.CaptureState();
            Check(game.Process(ConcedeTask.Any(opponent ? p.Opponent : p)) && p.Choice == null && !game.Resolution.HasSuspendedEffect &&
                !Generic.HasDecisionPlay(p) && p.Hero.Armor == stage && rng.Equals(game.Random.CaptureState()), "ConcedeAtEveryStep cancels remaining tails without RNG");
        });
    }

    private static void VisibleOffersOnly()
    {
        var game = Fixture(); var p = game.Player1;
        var unoffered = Generic.DrawCard(p, Cards.FromId("CS2_029"));
        var first = Generic.DrawCard(p, Cards.FromId("CORE_BT_801"));
        Generic.DrawCard(p, Cards.FromId("CORE_BT_801")); Core2026Proof.Play(game, p, "CATA_697");
        var before = game.SaveSnapshot();
        var answer = game.GetSelectorAnswer();
        var actions = FairBotState.Create(game, p.PlayerId, 90, DeckStyle.Value).Actions
            .OrderBy(a => a.ToString(), StringComparer.Ordinal).ToArray();
        var offeredActions = p.Choice.Choices.Select(id => BotAction.From(ChooseTask.Pick(p, id)))
            .OrderBy(a => a.ToString(), StringComparer.Ordinal).ToArray();
        Check(offeredActions.Length == 2 && actions.SequenceEqual(offeredActions), "fair selector exposes exactly the visible offers");
        Check(answer.SelectedOptionIds.Single() == first.Id && before.SequenceEqual(game.SaveSnapshot()), "visible first offered hand answer, bot reads isolated");
        foreach (var hidden in p.DeckZone.Concat(p.Opponent.HandZone).Concat(p.Opponent.DeckZone).ToArray())
            new TransformTask("CS2_171", EntityType.TARGET).Process(game, hidden.Controller, p.Hero, hidden);
        new TransformTask("CS2_106", EntityType.TARGET).Process(game, p, p.Hero, unoffered);
        Check(game.GetSelectorAnswer().SelectedOptionIds.Single() == first.Id &&
            FairBotState.Create(game, p.PlayerId, 91, DeckStyle.Value).Actions
                .OrderBy(a => a.ToString(), StringComparer.Ordinal).SequenceEqual(actions),
            "VisibleOffersOnly: permute unoffered own/hidden cards, same answer and offered action set");
    }

    private static void SelectorFreshProcessRestore()
    {
        string file = Path.Combine(Path.GetTempPath(), "hs-selector-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (string id in new[] { "CATA_697", "BE_036" })
            {
                var game = Fixture(19, true); Generic.DrawCard(game.Player1, Cards.FromId("CORE_BT_801"));
                Core2026Proof.Play(game, game.Player1, id);
                for (int stage = 0; stage < 2; stage++)
                {
                    var bytes = game.SaveSnapshot(); File.WriteAllBytes(file, bytes);
                    File.WriteAllText(file + ".hash", Future(Game.LoadSnapshot(bytes)));
                    var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath) { RedirectStandardOutput = true, RedirectStandardError = true };
                    if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    start.ArgumentList.Add("--player-decision-child"); start.ArgumentList.Add(file);
                    using var child = System.Diagnostics.Process.Start(start);
                    string output = child.StandardOutput.ReadToEnd(), error = child.StandardError.ReadToEnd();
                    Check(child.WaitForExit(30000) && child.ExitCode == 0, "SelectorFreshProcessRestore: " + output + error);
                    Answer(game, game.Player1.Choice.Choices[0]);
                }
            }
        }
        finally { File.Delete(file); File.Delete(file + ".hash"); }
    }

    internal static void Run() => WithHandRecipe(RunCore);

    private static void RunCore()
    {
        FilteredHand(); CandidateChangesBetweenSteps(); ExcludePhysicalEntity(); EmptySelectorContinues(); BoardSelectors();
        SourceDiesTransformsBounces(); ConcedeAtEveryStep(); VisibleOffersOnly(); SelectorFreshProcessRestore();
        DecisionProof.SelectorRejectsWithoutMutation(); SnapshotProof.SelectorCloneIsolation(); SnapshotProof.CorruptSelectorDescriptorRejected();
        Console.WriteLine("PASS filtered selectors: Hand/Board, filters, physical exclusions, fresh repeated offers, empty Continue/StopRecipe, singleton prompts, source changes, concede every step, pure rejection, clone/fresh-process restore, descriptor corruption, visible-offer bot policy");
        Check(Cards.FromId("BE_566").Implemented, "batch-14 Wriggler is implemented");
        foreach (int seed in new[] {1, 2, 3}) foreach (int count in new[] {0, 1, 3}) foreach (bool brann in new[] {false, true}) Hand(seed, count, brann);
        ShuffleReactionsBeforeRepeat(); DeathsWaitForBattlecryCompletion();
        StaleAndConcede(); SourceRemoved(); Fixed(); Replay(); AfterPlayAndEcho(); FreshProcess();
        Console.WriteLine("PASS player decisions: hand/fixed, empty/singleton/duplicate, Brann/Tak reaction order, deferred deaths/auras, replay, clone/snapshot, stale/concede, no selection RNG");
    }
}
