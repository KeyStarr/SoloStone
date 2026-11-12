using System.Diagnostics;
using System.Reflection;
using HsSim;
using SabberStoneCore.Auras;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Model.Rewind;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using static Core2026Proof;

internal static class DecisionSpellProof
{
    private static void Check(bool ok, string message) => Core2026Proof.Check(ok, "Spell decision: " + message);
    private static Game Fixture() => PlayerDecisionProof.Fixture(19);
    private static void Deck(Controller p, params string[] ids)
    {
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        foreach (string id in ids.Reverse()) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId(id)));
    }
    private static void Balanced(Game g, int initialDepth = 0)
    {
        int depth = initialDepth;
        foreach (var entry in g.PowerHistory.Full)
        {
            if (entry is PowerHistoryBlockStart) depth++;
            if (entry is PowerHistoryBlockEnd) depth--;
            Check(depth >= 0, "history has no unmatched end");
        }
        Check(depth == (g.SpellCompletion == null ? 0 : 2) && g.Resolution.Depth == 0 && g.TaskQueue.IsQuiescent, "only resolving spell PLAY/POWER remain open at decision boundary; resolution/events quiescent");
    }
    private static void Reject(Game g, DecisionAnswer answer)
    {
        var before = g.SaveSnapshot();
        Check(!g.TryAnswer(answer, out _) && before.SequenceEqual(g.SaveSnapshot()), "stale answer rejected without state/history/RNG mutation");
    }
    private static void RoundTrip(Game g, int pick)
    {
        Balanced(g);
        var bytes = g.SaveSnapshot();
        var copy = g.CloneExact(history: true); var restored = Game.LoadSnapshot(bytes);
        Check(bytes.SequenceEqual(restored.SaveSnapshot()), "snapshot byte roundtrip");
        var d = g.GetPendingDecisions().Single();
        foreach (var invalid in new[] { new DecisionAnswer(d.Id + 1, d.ControllerId, new[] {pick}),
            new DecisionAnswer(d.Id, g.Player2.Id, new[] {pick}), new DecisionAnswer(d.Id, d.ControllerId, new[] {g.Player1.Hero.Id}) }) Reject(g, invalid);
        foreach (var candidate in new[] { g, copy, restored }) PlayerDecisionProof.Answer(candidate, pick);
        Check(Canonical.State(g) == Canonical.State(copy) && Canonical.State(g) == Canonical.State(restored) &&
            Canonical.Value(g.PowerHistory.Last) == Canonical.Value(copy.PowerHistory.Last) && Canonical.History(g) == Canonical.History(restored) &&
            g.Random.CaptureState().Equals(copy.Random.CaptureState()) && g.Random.CaptureState().Equals(restored.Random.CaptureState()),
            "clone and snapshot answer have identical state/history/RNG");
        Reject(g, new DecisionAnswer(d.Id, d.ControllerId, new[] {pick})); Balanced(g);
    }

    internal static void DarkBribe(Game g, Controller p, string id)
    {
        var preexisting = Add(p, "CS2_182");
        Deck(p, "CS2_182", "CS2_182", "CS2_029");
        var expected = p.DeckZone.Reverse().Select(c => c.Id).ToArray();
        var selected = p.DeckZone.TopCard;
        Generic.AddEnchantmentBlock(g, Cards.FromId("EX1_004e"), p.Hero, selected);
        new ApplyEffectTask(EntityType.TARGET, Effects.ReduceCost(1)).Process(g, p, p.Hero, selected);
        int attack = ((Minion)selected).AttackDamage, health = ((Minion)selected).Health, cost = selected.Cost;
        var source = Play(g, p, id);
        Check(p.Choice.Choices.SequenceEqual(expected) && !p.Choice.Choices.Contains(preexisting.Id), "CapturedDrawsOnly: physical draws, not existing same-ID card");
        Check(source.Zone != p.GraveyardZone && p.UsedMana == 2 && p.NumSpellsPlayedThisGame == 0 && !p.IsComboActive,
            "DecisionSpellPaysOnce: paid, hand removed, spell/card tails deferred");
        Check(g.GetSelectorAnswer().SelectedOptionIds.Single() == expected[0], "captured selector fallback picks first eligible in draw order");
        // Since d365fa79 search considers every eligible captured draw; first-valid is only the fallback.
        var before = g.SaveSnapshot();
        var view = FairBotState.Create(g, p.PlayerId, 77, DeckStyle.Value);
        var offers = p.Choice.Choices.Select(choice => BotAction.From(ChooseTask.Pick(p, choice)).ToString()).Order(StringComparer.Ordinal).ToArray();
        Check(view.Actions.Select(a => a.ToString()).Order(StringComparer.Ordinal).SequenceEqual(offers),
            "captured bot search exposes every eligible draw exactly once and no ordinary actions");
        foreach (var action in view.Actions) view.Apply(action);
        Check(before.SequenceEqual(g.SaveSnapshot()), "captured selector search preserves authoritative state");
        RoundTrip(g, selected.Id);
        Check(selected.Controller == p.Opponent && selected[GameTag.CONTROLLER] == p.Opponent.PlayerId && selected.Zone == p.Opponent.HandZone &&
            ((Minion)selected).AttackDamage == attack && ((Minion)selected).Health == health && selected.Cost == cost && selected.AppliedEnchantments.Count == 1,
            "transfer preserves identity, enchantments, stats and cost");
        Check(source.Zone == p.GraveyardZone && p.UsedMana == 2 && p.NumSpellsPlayedThisGame == 1 && p.IsComboActive &&
            p.NumCardsPlayedThisTurn == 1 && p.PlayHistory.Count == 1 && g.SpellCompletion == null, "cast/play/card finish once");
        End(g); p.Opponent.BaseMana = 10; p.Opponent.UsedMana = 0;
        Check(g.Process(PlayCardTask.Any(p.Opponent, selected)) && selected.Zone == p.Opponent.BoardZone, "given enchanted card playable by opponent");
    }
    internal static void FastForward(Game g, Controller p, string id)
    {
        Deck(p, "CS2_182", "CS2_029"); var expected = p.DeckZone.Reverse().Select(c => c.Id).ToArray();
        var source = Play(g, p, id); RoundTrip(g, expected[0]);
        Check(g.IdEntityDic[expected[0]].Cost == 2 && g.IdEntityDic[expected[1]].Cost == 4 && source.Zone == p.GraveyardZone,
            "Fast Forward discounts only selected draw by two");
        Deck(p, "CS2_231"); Play(g, p, id); int pick = p.Choice.Choices.Single();
        PlayerDecisionProof.Answer(g, pick); Check(g.IdEntityDic[pick].Cost == 0, "cost floor zero; singleton still prompts");
    }
    internal static void EarthenRoar(Game g, Controller p, string id)
    {
        var first = Summon(p.Opponent, "CS2_182"); var second = Summon(p.Opponent, "CS2_182"); var friendly = Summon(p, "CS2_182");
        StandardSetsProof.TargetRules(g, p, id, first, friendly, true);
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        var elusive = Summon(p.Opponent, "CS2_182"); elusive.CantBeTargetedBySpells = true;
        first.HasDivineShield = true; first.Damage = 1;
        Generic.AddEnchantmentBlock(g, Cards.FromId("EX1_004e"), p.Hero, first);
        Play(g, p, id, first);
        Check(first.Health == 1 && first.BaseHealth == 1 && first.Damage == 0 && first.HasDivineShield && p.Choice == null && second.Health == 5,
            "no Dragon: set current/max Health, preserve Shield, no damage or second pick");
        Add(p, "CATA_484");
        Play(g, p, id, first);
        Check(p.Choice.Choices.SequenceEqual(new[] {second.Id}) && !p.Choice.Choices.Contains(first.Id) && !p.Choice.Choices.Contains(friendly.Id),
            "holding Dragon: singleton second enemy minion; first target/friendly/heroes excluded");
        RoundTrip(g, second.Id); Check(second.Health == 1 && second.BaseHealth == 1, "second Health set once");
        Kill(g, second); Play(g, p, id, first); Check(p.Choice == null, "Dragon with no other enemy finishes without prompt");
    }
    private static void CapturedDrawsOnly()
    {
        foreach (string id in new[] { "JAIL_206", "TIME_770" })
        {
            var g = Fixture(); var p = g.Player1; Deck(p);
            Play(g, p, id);
            Check(p.Choice == null && p.Hero.Damage == (id == "JAIL_206" ? 6 : 3), "fatigue attempts continue; zero candidates finish");
            g = Fixture(); p = g.Player1;
            for (int i = 0; i < 9; i++) Add(p, "CS2_182");
            Deck(p, "CS2_182", "CS2_182", "CS2_182"); int first = p.DeckZone.TopCard.Id;
            Play(g, p, id);
            Check(p.Choice.Choices.SequenceEqual(new[] {first}) && p.HandZone.Count == 10, "full-hand burns excluded; singleton offered");
            PlayerDecisionProof.Answer(g, first);
            g = Fixture(); p = g.Player1; Summon(p, "BRM_031");
            Deck(p, "CS2_182", "CS2_182", "CS2_182"); var physical = p.DeckZone.Reverse().Take(id == "JAIL_206" ? 3 : 2).Select(c => c.Id).ToArray();
            Play(g, p, id);
            Check(p.Choice.Choices.SequenceEqual(physical) && p.HandZone.Count == 2 * physical.Length, "draw-trigger copies never captured");
            PlayerDecisionProof.Answer(g, physical[0]);
        }
        var full = Fixture(); var player = full.Player1;
        while (!player.Opponent.HandZone.IsFull) Add(player.Opponent, "CS2_182");
        Play(full, player, "JAIL_206"); int selected = player.Choice.Choices[0]; PlayerDecisionProof.Answer(full, selected);
        Check(full.IdEntityDic[selected].Controller == player.Opponent && full.IdEntityDic[selected].Zone == player.Opponent.GraveyardZone && player.Opponent.HandZone.Count == 10,
            "opponent full hand burns transferred actual entity without discard");
    }
    private static void CapturedTopdeckAndStale()
    {
        foreach (string topdeck in new[] { "SW_439t", "CAP_400t2t", "TLC_513t2", "TIME_025t" })
        {
            var g = Fixture(); var p = g.Player1;
            Deck(p, topdeck, "CS2_182", "CS2_029");
            int replacement = p.DeckZone.Reverse().Skip(1).First().Id;
            int direct = p.DeckZone.Reverse().Skip(2).First().Id;
            Play(g, p, "TIME_770");
            Check(p.Choice.Choices.SequenceEqual(new[] {direct}) && p.HandZone.Any(c => c.Id == replacement),
                "cast/summon-on-draw and their replacement draws excluded " + topdeck);
            var decision = g.GetPendingDecisions().Single();
            var chosen = g.IdEntityDic[direct]; p.SetasideZone.Add(p.HandZone.Remove(chosen));
            Reject(g, new DecisionAnswer(decision.Id, p.Id, new[] {direct}));
            Check(g.Process(ConcedeTask.Any(p)), "concede after offered card leaves hand");
        }
    }
    private static void LethalFatigue()
    {
        var g = Fixture(); var p = g.Player1; Deck(p, "CS2_182"); p.Hero.Damage = 29;
        var source = Play(g, p, "JAIL_206");
        Check(g.State == State.COMPLETE && p.Choice == null && !g.Resolution.HasSuspendedEffect && g.SpellCompletion == null,
            "lethal fatigue cannot strand a decision or completion descriptor"); Balanced(g);
    }

    private static readonly DecisionSpellTask Terminal = new DecisionSpellTask("proof/spell/terminal/v1",
        new DrawAndCaptureTask(1), new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.CapturedEntities)),
        new DrawTask(1), new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.CapturedEntities)));

    private static readonly DecisionSpellTask DiscoverHellfire = new DecisionSpellTask("proof/spell/discover-hellfire/v1",
        new DiscoverThenContinueTask("CS2_182"), new DamageTask(3, EntityType.ALL, true));
    private static readonly DecisionSpellTask DiscoverHellfireThenChoice = new DecisionSpellTask("proof/spell/discover-hellfire-choice/v1",
        new DiscoverThenContinueTask("CS2_182"), new DamageTask(3, EntityType.ALL, true), new DiscoverThenContinueTask("CS2_182"));

    private static Game CorpseFixture()
    {
        var g = new Game(new GameConfig { RandomSeed = 19, StartPlayer = 1, SkipMulligan = true,
            EnableCorpses = true, History = true, Logging = false, Player1HeroClass = CardClass.DEATHKNIGHT,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        g.StartGame(); g.Player1.BaseMana = 10;
        return g;
    }

    internal static void LethalDeathCleanup(bool discover, bool pendingChoice = false, string loser = "opponent")
    {
        var card = Cards.FromId("CS2_062"); var original = card.Power;
        try
        {
            if (discover) card.Power = new Power { PowerTask = pendingChoice ? DiscoverHellfireThenChoice : DiscoverHellfire };
            var g = CorpseFixture(); var p = g.Player1;
            if (loser != "owner") p.Opponent.Hero.Damage = 29;
            if (loser != "opponent") p.Hero.Damage = 29;
            var wisp = Summon(p, "CS2_231");
            var source = Play(g, p, "CS2_062");
            if (discover)
            {
                Check(g.Resolution.HasSuspendedEffect && source[GameTag.ZONE] == (int)Zone.PLAY && wisp.Health == 1,
                    "Discover suspends before Hellfire damage");
                PlayerDecisionProof.Answer(g, p.Choice.Choices[0]);
            }
            // Both heroes dying together is a draw: TIED for both (df11d505, owner ruling 2026-10-27).
            var expectedOwner = loser == "opponent" ? PlayState.WON : loser == "tie" ? PlayState.TIED : PlayState.LOST;
            var expectedOpponent = loser == "owner" ? PlayState.WON : loser == "tie" ? PlayState.TIED : PlayState.LOST;
            Check(g.State == State.COMPLETE && p.PlayState == expectedOwner && p.Opponent.PlayState == expectedOpponent,
                "lethal Hellfire ends match: " + loser + " (" + p.PlayState + "/" + p.Opponent.PlayState + ")");
            Check(wisp.Health == -2 && wisp.Zone == p.GraveyardZone && g.DeadMinions.Count == 0,
                "lethal Hellfire settles committed Wisp death");
            Check(CorpseRules.Available(p) == 1 && p.NumSpellsPlayedThisGame == (pendingChoice ? 0 : 1) && p.IsComboActive == !pendingChoice &&
                p.NumOptionsPlayedThisTurn == (discover && !pendingChoice ? 2 : 1),
                "lethal Hellfire records corpse and only completed spell/card bookkeeping");
            Check(source.Zone == p.GraveyardZone && p.Choice == null && p.Opponent.Choice == null &&
                !g.Resolution.HasSuspendedEffect && g.SpellCompletion == null && g.GetPendingDecisions().Count == 0,
                "completed lethal spell leaves no choice or continuation");
            Balanced(g);
            Console.WriteLine($"PASS lethal Hellfire death cleanup (Discover={discover}, pending choice={pendingChoice}, loser={loser})");
        }
        finally { card.Power = original; }
    }

    private static void ConcedeDeathCleanup()
    {
        foreach (bool enemy in new[] { false, true })
        foreach (int rewind in new[] { 0, 1, 2 })
        {
            var g = CorpseFixture(); var p = g.Player1;
            var wisp = Summon(p, "CS2_231"); var pyro = Summon(p, "NEW1_020");
            var source = Add(p, "RLK_025");
            if (rewind != 0) source[GameTag.REWIND] = 1;
            Check(g.Process(PlayCardTask.Any(p, source, wisp)) && p.Choice != null && wisp.Health == -2 &&
                wisp.Zone == p.BoardZone && g.DeadMinions.Count == 1, "Frost Strike parks committed death at Discover");
            using (g.Resolution.Enter(ResolutionKind.EffectResolution))
            {
                var state = Canonical.State(g); var history = Canonical.History(g) + Canonical.Value(g.PowerHistory.Last);
                var random = g.Random.CaptureState();
                bool rejected = false;
                try { g.Process(ConcedeTask.Any(p)); }
                catch (NotSupportedException) { rejected = true; }
                Check(rejected && g.State == State.RUNNING && p.Choice != null && g.SpellCompletion != null,
                    "in-flight concession rejects before terminal mutation");
                Check(state == Canonical.State(g) && history == Canonical.History(g) + Canonical.Value(g.PowerHistory.Last) &&
                    random.Equals(g.Random.CaptureState()), "rejected concession preserves pending state/history/RNG");
            }
            if (rewind != 0)
            {
                var bytes = g.SaveSnapshot();
                var decision = g.GetPendingDecisions().Single();
                Check(g.Rewind?.Stage == RewindStage.Attempt && p.Choice.ChoiceAction != ChoiceAction.REWIND &&
                    !g.TryAnswer(new DecisionAnswer(decision.Id, p.Id, new[] { p.Hero.Id }), out _) &&
                    bytes.SequenceEqual(g.SaveSnapshot()), "Rewind cannot reset deferred body before its offer");
                if (rewind == 2)
                {
                    PlayerDecisionProof.Answer(g, p.Choice.Choices[0]);
                    Check(CorpseRules.Available(p) == 1 && g.Rewind?.Stage == RewindStage.Offered,
                        "completed Frost Strike settles death before Rewind offer");
                    RewindTestHelpers.AnswerRewind(g);
                    Check(g.Rewind?.Stage == RewindStage.Attempt && g.SpellCompletion != null && g.Player1.Choice != null &&
                        g.DeadMinions.Count == 1 && CorpseRules.Available(g.Player1) == 0,
                        "Rewind reset restores corpse baseline then replay defers death again");
                }
            }
            // Exact clones retain open block context but start a fresh history log.
            foreach (var (game, depth) in new[] { (g, 0), (g.CloneExact(history: true), 2), (Game.LoadSnapshot(g.SaveSnapshot()), 0) })
            {
                var owner = game.Player1; var random = game.Random.CaptureState();
                Check(game.Process(ConcedeTask.Any(enemy ? owner.Opponent : owner)), "concede during Frost Strike accepted");
                Check(game.State == State.COMPLETE && owner.PlayState == (enemy ? PlayState.WON : PlayState.LOST) &&
                    owner.Opponent.PlayState == (enemy ? PlayState.LOST : PlayState.WON), "concede preserves winner");
                Check(game.IdEntityDic[wisp.Id].Zone == owner.GraveyardZone && game.DeadMinions.Count == 0 &&
                    CorpseRules.Available(owner) == 1 && owner.NumFriendlyMinionsThatDiedThisTurn == 1 &&
                    game.NumMinionsKilledThisTurn == 1, "concede settles Wisp graveyard/corpse/death counters");
                Check(game.IdEntityDic[source.Id].Zone == owner.GraveyardZone && game.SpellCompletion == null &&
                    !game.Resolution.HasSuspendedEffect && owner.Choice == null && owner.Opponent.Choice == null &&
                    game.Rewind == null && game.GetPendingDecisions().Count == 0, "concede drops spell/choice/Rewind continuations");
                Check(owner.NumSpellsPlayedThisGame == 0 && owner.NumCardsPlayedThisTurn == 1 &&
                    ((Minion)game.IdEntityDic[pyro.Id]).Damage == 0 && random.Equals(game.Random.CaptureState()),
                    "concede never completes abandoned spell or after-cast tails");
                Balanced(game, depth);
            }
            Console.WriteLine($"PASS Frost Strike concession death cleanup (enemy={enemy}, Rewind stage={rewind})");
        }
    }

    private static void ForcedTerminalDeathCleanup()
    {
        var g = CorpseFixture(); var p = g.Player1;
        var wisp = Summon(p, "CS2_231"); var source = Play(g, p, "RLK_025", wisp);
        p.PlayState = PlayState.LOSING;
        g.NextStep = Step.FINAL_WRAPUP;
        Check(g.State == State.COMPLETE && wisp.Zone == p.GraveyardZone && CorpseRules.Available(p) == 1 &&
            g.DeadMinions.Count == 0 && source.Zone == p.GraveyardZone && g.SpellCompletion == null &&
            !g.Resolution.HasSuspendedEffect && p.Choice == null && p.NumSpellsPlayedThisGame == 0,
            "direct terminal step settles deferred deaths without another Process call");
        int entries = g.PowerHistory.Full.Count;
        g.FinalGameOver();
        Check(CorpseRules.Available(p) == 1 && g.NumMinionsKilledThisTurn == 1 && entries == g.PowerHistory.Full.Count,
            "terminal settlement is idempotent");
        Balanced(g);
        Console.WriteLine("PASS forced terminal step: committed death cleanup once, abandoned spell never completed");
    }

    internal static void TerminalBetweenChoices()
    {
        ConcedeDeathCleanup();
        ForcedTerminalDeathCleanup();
        foreach (string loser in new[] { "opponent", "owner", "tie" })
        {
            LethalDeathCleanup(false, loser: loser); LethalDeathCleanup(true, loser: loser);
            LethalDeathCleanup(true, pendingChoice: true, loser: loser);
        }
        foreach (bool lastChoice in new[] { false, true })
        {
            var g = Fixture(); var p = g.Player1; Deck(p); p.Hero.Damage = 29;
            var pyro = Summon(p, "NEW1_020");
            var source = Add(p, "LOOT_054");
            source.AuraEffects = new AuraEffects(CardType.SPELL); source.AuraEffects.Echo = true;
            Check(g.Process(PlayCardTask.Any(p, source)), "Echo Branching Paths accepted");
            Check(p.Choice != null && g.SpellCompletion != null, "Branching Paths waits for first choice");
            if (lastChoice) PlayerDecisionProof.Answer(g,
                p.Choice.Choices.Single(id => g.IdEntityDic[id].Card.Id == "LOOT_054b"));
            int draw = p.Choice.Choices.Single(id => g.IdEntityDic[id].Card.Text.Contains("Draw"));
            PlayerDecisionProof.Answer(g, draw);
            Check(g.State == State.COMPLETE && p.PlayState == PlayState.LOST, "choice fatigue ends game");
            Check(p.Choice == null && p.Opponent.Choice == null && !g.Resolution.HasSuspendedEffect && g.SpellCompletion == null,
                "terminal choice cancels remaining choices and spell completion");
            Balanced(g);
            Check(source.Zone == p.GraveyardZone && pyro.Damage == 0 && p.NumSpellsPlayedThisGame == (lastChoice ? 1 : 0),
                "finished text counts once; abandoned choice and after-cast callbacks never run");
            Check(!p.HandZone.Any(card => card[GameTag.GHOSTLY] == 1), "terminal card completion never creates Echo copy");
        }
        var card = Cards.FromId("EX1_238"); var original = card.Power;
        try
        {
            card.Power = new Power { PowerTask = Terminal };
            var g = Fixture(); var p = g.Player1; Deck(p, "CS2_182"); p.Hero.Damage = 29;
            var pyro = Summon(p, "NEW1_020");
            var source = Play(g, p, "EX1_238", p.Opponent.Hero);
            PlayerDecisionProof.Answer(g, p.Choice.Choices[0]);
            Check(g.State == State.COMPLETE && p.Choice == null && !g.Resolution.HasSuspendedEffect && g.SpellCompletion == null,
                "terminal resumed recipe cancels cursor and completion");
            Check(source.Zone == p.GraveyardZone && pyro.Damage == 0 && p.NumSpellsPlayedThisGame == 0 && p.OverloadOwed == 0,
                "terminal resumed recipe skips overload and after-cast tails");
            Balanced(g);
        }
        finally { card.Power = original; }
        Console.WriteLine("PASS terminal between choices closes spell history without tails");
    }

    internal static void FullHandTwinspell()
    {
        var g = Fixture(); var p = g.Player1; Deck(p, "CS2_182");
        var drawn = p.DeckZone.TopCard;
        var acolyte = Summon(p, "EX1_007");
        var source = Add(p, "DAL_373");
        while (p.HandZone.Count < 10) Add(p, "CS2_231");
        Check(g.Process(PlayCardTask.Any(p, source, acolyte)), "full-hand Rapid Fire accepted");
        Check(p.HandZone.Count(c => c.Card.Id == "DAL_373ts") == 1 && p.HandZone.Count == 10 && drawn.Zone == p.GraveyardZone,
            "Twinspell copy takes hand slot before Acolyte draw burns");
        Check(acolyte.Damage == 1 && source.Zone == p.GraveyardZone && p.NumSpellsPlayedThisGame == 1,
            "Rapid Fire resolves once");
        Balanced(g);
        Console.WriteLine("PASS full-hand Twinspell keeps copy and burns reactive draw");
    }

    private static readonly DecisionSpellTask Nested = new DecisionSpellTask("proof/spell/nested/v1",
        new CustomTask((g, c, s, t, stack) => Generic.CastSpell(c, g,
            (Spell)Entity.FromCard(c, Cards.FromId("TIME_770")), null, 0)),
        new DrawAndCaptureTask(2),
        StateTaskList.SelectRepeated("proof/spell/nested-picks/v1", new SelectorSpec(SelectorSource.CapturedEntities), 2, new ArmorTask(1)));
    private static void NestedAutomaticAndCapture()
    {
        var card = Cards.FromId("EX1_238"); var power = card.Power;
        try
        {
            card.Power = new Power { PowerTask = Nested };
            var g = Fixture(); var p = g.Player1; Play(g, p, "EX1_238", p.Opponent.Hero);
            Check(p.Choice != null && p.Choice.Choices.Count == 2 && p.HandZone.Count == 4, "nested automatic cast returns before outer captured selector");
            RoundTrip(g, p.Choice.Choices[0]); RoundTrip(g, p.Choice.Choices[0]);
            Check(p.Hero.Armor == 2 && p.NumSpellsPlayedThisGame == 1, "nested captured recipe resumes caller and finishes once");
        }
        finally { card.Power = power; }
    }

    private static readonly DecisionSpellTask Repeated = new DecisionSpellTask("proof/spell/repeated/v1",
        new CustomTask((g, c, s, t, stack) => { if (c.Hero.Weapon != null) c.Hero.Weapon.ToBeDestroyed = true; }),
        new DrawAndCaptureTask(2),
        new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.CapturedEntities)),
        new ArmorTask(1),
        new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.CapturedEntities)),
        new ArmorTask(1));

    private static void WithRepeated(Action proof)
    {
        var card = Cards.FromId("EX1_238"); var power = card.Power;
        try { card.Power = new Power { PowerTask = Repeated }; proof(); }
        finally { card.Power = power; }
    }
    private static void CompletionAndCleanup() => WithRepeated(() => {
        var g = Fixture(); var p = g.Player1;
        var weapon = (Weapon)Entity.FromCard(p, Cards.FromId("CS2_106")); p.Hero.AddWeapon(weapon);
        var pyro = Summon(p, "NEW1_020"); var sherazin = Summon(p, "UNG_065t");
        var source = Add(p, "EX1_238"); source.AuraEffects = new AuraEffects(CardType.SPELL); source.AuraEffects.Echo = true;
        Check(g.Process(PlayCardTask.Any(p, source, p.Opponent.Hero)), "decision overload spell plays");
        var saved = g.SaveSnapshot(); // Pending destroyed weapon must survive this boundary.
        Check(pyro.Damage == 0 && p.OverloadOwed == 0 && source.Zone != p.GraveyardZone && sherazin[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0,
            "after-cast/after-play/overload/graveyard deferred");
        RoundTrip(g, p.Choice.Choices[0]);
        Check(p.Hero.Armor == 1 && p.Choice != null && p.UsedMana == 1 && p.OverloadOwed == 0, "intermediate step does not run tails");
        RoundTrip(g, p.Choice.Choices[0]);
        Check(p.Hero.Armor == 2 && p.UsedMana == 1 && p.OverloadOwed == 1 && p.OverloadThisGame == 1 && source.Zone == p.GraveyardZone &&
            pyro.Damage == 1 && sherazin[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && p.NumSpellsPlayedThisGame == 1 &&
            p.HandZone.Count(c => c.Card.Id == "EX1_238" && c[GameTag.GHOSTLY] == 1) == 1 && p.Hero.Weapon == null && weapon.Zone == p.GraveyardZone,
            "DecisionSpellFinishesOnce: overload, after-cast, after-play, Echo, graveyard and weapon cleanup exactly once");
    });
    private static void CounteredSpellNeverPrompts()
    {
        var g = Fixture(); var p = g.Player1;
        End(g); Play(g, p.Opponent, "EX1_287"); End(g);
        int deck = p.DeckZone.Count; var source = Play(g, p, "JAIL_206");
        Check(((Spell)source).IsCountered && p.Choice == null && g.SpellCompletion == null && p.DeckZone.Count == deck && source.Zone == p.GraveyardZone,
            "CounteredSpellNeverPrompts"); Balanced(g);
    }
    private static void AutomaticCastCompletes()
    {
        foreach (string id in new[] { "JAIL_206", "TIME_770", "CATA_554" })
        {
            var g = Fixture(); var p = g.Player1;
            var target = id == "CATA_554" ? Summon(p.Opponent, "CS2_182") : null;
            if (target != null) { Summon(p.Opponent, "CS2_182"); Add(p, "CATA_484"); }
            var source = (Spell)Entity.FromCard(p, Cards.FromId(id));
            Generic.CastSpell(p, g, source, target, 0); g.DeathProcessingAndAuraUpdate();
            Check(p.Choice == null && !g.Resolution.HasSuspendedEffect && g.SpellCompletion == null && source.Zone == p.GraveyardZone &&
                p.UsedMana == 0 && p.NumSpellsPlayedThisGame == 0 && p[Generic.AutomaticDecisionTag] == 0,
                "AutomaticCastCompletes synchronously without hand-play bookkeeping " + id);
        }
    }
    private static void AutomaticCastFromChoice()
    {
        foreach (var action in new[] { ChoiceAction.CAST, ChoiceAction.SPELL_RANDOM })
        foreach (string id in new[] { "JAIL_206", "TIME_770", "CATA_554" })
        {
            var g = Fixture(); var p = g.Player1;
            Deck(p, "CS2_182", "CS2_182", "CS2_182");
            Add(p, "CATA_484");
            var first = Summon(p.Opponent, "CS2_182"); var second = Summon(p.Opponent, "CS2_182");
            Choice parent = null;
            Generic.CreateChoiceCards(p, p.Hero, null, ChoiceType.GENERAL, action,
                new[] { Cards.FromId(id) }, new CustomTask((game, c, s, t, stack) => {
                    Check(ReferenceEquals(c.Choice, parent), "automatic cast restores parent for Discover after-task");
                    c.Hero.Armor++;
                }));
            parent = p.Choice;
            Generic.CreateChoiceCards(p, p.Hero, null, ChoiceType.GENERAL, ChoiceAction.HAND,
                new[] { Cards.FromId("CS2_231") }, null);
            var next = parent.NextChoice;
            var source = g.IdEntityDic[parent.Choices.Single()];
            Check(g.Process(ChooseTask.Pick(p, source.Id)), "cast-from-Discover callback accepts answer " + action + " " + id);
            Check(ReferenceEquals(p.Choice, next) && p.Hero.Armor == 1 && !g.Resolution.HasSuspendedEffect &&
                g.SpellCompletion == null && source.Zone == p.GraveyardZone && p.UsedMana == 0 && p[Generic.AutomaticDecisionTag] == 0,
                "automatic choice cast completes synchronously and preserves queued parent choice " + action + " " + id);
            if (id == "JAIL_206") Check(p.Opponent.HandZone.Any(c => c.Card.Id == "CS2_182"), "choice-cast Dark Bribe gives captured draw");
            if (id == "TIME_770") Check(p.HandZone.Any(c => c.Card.Id == "CS2_182" && c.Cost == 2), "choice-cast Fast Forward discounts captured draw");
            if (id == "CATA_554") Check(first.Health == 1 || second.Health == 1, "choice-cast Earthen Roar automatically picks enemy");
            Check(g.Process(ChooseTask.Pick(p, next.Choices.Single())) && p.Choice == null, "queued Discover choice remains answerable");
            Balanced(g);
        }
    }
    private static void TransferOwnership()
    {
        var enchant = Cards.FromId("EX1_004e"); var original = enchant.Power;
        try
        {
            enchant.Power = new Power {
                Trigger = new Trigger(TriggerType.TAKE_DAMAGE) { TriggerSource = TriggerSource.HERO,
                    EitherTurn = true, SingleTask = new ArmorTask(1) },
                Aura = new Aura(AuraType.HAND, Effects.ReduceCost(1)) };
            var g = Fixture(); var p = g.Player1;
            Summon(p, "EX1_608"); Summon(p.Opponent, "EX1_608"); Summon(p.Opponent, "EX1_608");
            var donor = Add(p, "CS2_029"); var receiver = Add(p.Opponent, "CS2_029");
            Deck(p, "CS2_029", "CS2_029", "CS2_029");
            Play(g, p, "JAIL_206"); var selected = g.IdEntityDic[p.Choice.Choices[0]];
            Generic.AddEnchantmentBlock(g, enchant, p.Hero, selected); g.DeathProcessingAndAuraUpdate();
            var attached = selected.AppliedEnchantments.Single();
            Check(selected.Cost == 2 && donor.Cost == 2 && receiver.Cost == 2, "donor hand aura applies before give");
            PlayerDecisionProof.Answer(g, selected.Id);
            Check(attached.Controller == p.Opponent && attached[GameTag.CONTROLLER] == p.Opponent.PlayerId &&
                attached.Creator == p.Hero && attached.Zone == p.Opponent.BoardZone,
                "give rebinds enchantment ownership and keeps original Creator");
            Check(selected.Cost == 1 && donor.Cost == 3 && receiver.Cost == 1,
                "give removes donor cost auras and applies receiver and transferred enchantment hand auras");
            new DamageTask(1, EntityType.HERO).Process(g, p, p.Hero, null); g.ProcessTasks();
            Check(p.Hero.Armor == 0 && p.Opponent.Hero.Armor == 0, "transferred enchantment unsubscribes donor hero");
            new DamageTask(1, EntityType.HERO).Process(g, p.Opponent, p.Opponent.Hero, null); g.ProcessTasks();
            Check(p.Hero.Armor == 0 && p.Opponent.Hero.Armor == 1, "transferred enchantment subscribes receiver hero exactly once");
            attached.Remove(); g.DeathProcessingAndAuraUpdate();
            Check(selected.Cost == 2 && donor.Cost == 3 && receiver.Cost == 2, "transferred aura removes cleanly on receiver");
        }
        finally { enchant.Power = original; }
    }
    private static readonly DecisionSpellTask Twin = new DecisionSpellTask("proof/spell/twin/v1",
        new DrawAndCaptureTask(1), new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.CapturedEntities)), new ArmorTask(1));
    private static void Twinspell()
    {
        var card = Cards.FromId("DAL_577"); var power = card.Power;
        try
        {
            card.Power = new Power { PowerTask = Twin };
            var g = Fixture(); var p = g.Player1; var target = Summon(p.Opponent, "CS2_182");
            var source = Play(g, p, "DAL_577", target);
            Check(p.HandZone.Count(c => c.Card.Id == "DAL_577ts") == 1, "Twinspell copy created before choice-opening text resolves");
            RoundTrip(g, p.Choice.Choices[0]);
            Check(p.HandZone.Count(c => c.Card.Id == "DAL_577ts") == 1 && p.Hero.Armor == 1 && source.Zone == p.GraveyardZone,
                "Twinspell copy and recipe resolve once");
            g = Fixture(); p = g.Player1; target = Summon(p.Opponent, "CS2_182");
            Deck(p, "CS2_182"); var drawn = p.DeckZone.TopCard;
            for (int i = 0; i < 9; i++) Add(p, "CS2_231");
            Play(g, p, "DAL_577", target);
            Check(p.HandZone.Count(c => c.Card.Id == "DAL_577ts") == 1 && drawn.Zone == p.GraveyardZone && p.Choice == null,
                "choice recipe Twinspell copy takes hand slot before text draw burns");
            Balanced(g);
        }
        finally { card.Power = power; }
    }
    private static void RedirectedFirstTarget()
    {
        var g = Fixture(); var p = g.Player1; End(g); Play(g, p.Opponent, "tt_010"); End(g);
        Add(p, "CATA_484"); var original = Summon(p.Opponent, "CS2_182");
        var source = Play(g, p, "CATA_554", original);
        int effective = source.CardTarget;
        Check(effective != original.Id && ((Minion)g.IdEntityDic[effective]).Health == 1 &&
            p.Choice.Choices.SequenceEqual(new[] {original.Id}), "Spellbender: effective first target excluded, original target eligible");
        RoundTrip(g, original.Id); Check(original.Health == 1, "redirected target continuation finishes");
    }

    private static void ConcedeAtEveryStep() => WithRepeated(() => {
        for (int step = 0; step < 2; step++) foreach (bool enemy in new[] { false, true })
        {
            var g = Fixture(); var p = g.Player1; Play(g, p, "EX1_238", p.Opponent.Hero);
            for (int i = 0; i < step; i++) PlayerDecisionProof.Answer(g, p.Choice.Choices[0]);
            var rng = g.Random.CaptureState(); Check(g.Process(ConcedeTask.Any(enemy ? p.Opponent : p)), "concede accepted");
            Check(p.Choice == null && !g.Resolution.HasSuspendedEffect && g.SpellCompletion == null && p.Hero.Armor == step && p.OverloadOwed == 0 &&
                rng.Equals(g.Random.CaptureState()), "ConcedeAtEveryStep drops recipe and tails without RNG"); Balanced(g);
        }
    });
    private static string Future(Game g)
    {
        while (g.Resolution.HasSuspendedEffect) PlayerDecisionProof.Answer(g, g.CurrentPlayer.Choice.Choices[0]);
        return Canonical.State(g) + Canonical.Value(g.PowerHistory.Last) + Canonical.Value(g.Random.CaptureState());
    }
    internal static void Child(string file)
    {
        var bytes = File.ReadAllBytes(file); var g = Game.LoadSnapshot(bytes);
        Check(bytes.SequenceEqual(g.SaveSnapshot()) && Future(g) == File.ReadAllText(file + ".future"), "fresh-process restore future");
    }
    private static void FreshProcess()
    {
        string file = Path.Combine(Path.GetTempPath(), "hs-spell-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (string id in new[] { "JAIL_206", "TIME_770", "CATA_554" })
            {
                var g = Fixture(); var p = g.Player1; Minion target = null;
                if (id == "CATA_554") { target = Summon(p.Opponent, "CS2_182"); Summon(p.Opponent, "CS2_182"); Add(p, "CATA_484"); }
                Play(g, p, id, target); var bytes = g.SaveSnapshot(); File.WriteAllBytes(file, bytes);
                File.WriteAllText(file + ".future", Future(g.CloneExact(history: true)));
                var start = new ProcessStartInfo(Environment.ProcessPath) { RedirectStandardOutput = true, RedirectStandardError = true };
                if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
                start.ArgumentList.Add("--spell-decision-child"); start.ArgumentList.Add(file);
                using var child = Process.Start(start); string output = child.StandardOutput.ReadToEnd(), error = child.StandardError.ReadToEnd();
                Check(child.WaitForExit(30000) && child.ExitCode == 0, "fresh-process " + id + output + error);
                Check(Future(g) == File.ReadAllText(file + ".future"), "original future equals fresh process");
            }
        }
        finally { File.Delete(file); File.Delete(file + ".future"); }
    }
    internal static void Run()
    {
        TerminalBetweenChoices(); FullHandTwinspell(); TransferOwnership(); AutomaticCastFromChoice();
        foreach (string id in new[] { "JAIL_206", "TIME_770", "CATA_554" })
        {
            var g = Fixture(); var p = g.Player1;
            if (id == "JAIL_206") DarkBribe(g, p, id);
            else if (id == "TIME_770") FastForward(g, p, id);
            else EarthenRoar(g, p, id);
        }
        CapturedDrawsOnly(); CapturedTopdeckAndStale(); NestedAutomaticAndCapture(); LethalFatigue(); CompletionAndCleanup(); CounteredSpellNeverPrompts(); AutomaticCastCompletes(); Twinspell(); RedirectedFirstTarget(); ConcedeAtEveryStep(); FreshProcess();
        Console.WriteLine("PASS spell decisions: pays/finishes once, capture, automatic/counter, cleanup, concede, stale, clone/snapshot/fresh-process");
    }
}
