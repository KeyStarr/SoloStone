using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static partial class RewindProof
{
    private static Game ReportFixture(int seat)
    {
        var game = PinnedPoolFixture.Use(ImbueProof.Make(CardClass.ROGUE, 20));
        if (seat == 2) End(game);
        foreach (var card in game.CurrentPlayer.HandZone.ToArray())
            game.CurrentPlayer.SetasideZone.Add(game.CurrentPlayer.HandZone.Remove(card));
        game.CurrentPlayer.BaseMana = 10;
        return game;
    }

    private static void MorchieHerald()
    {
        foreach (int seat in new[] { 1, 2 })
        foreach (string identity in new[] { "CORE_EDR_004", "CORE_EDR_004_2026" })
        {
            var game = ReportFixture(seat); var player = game.CurrentPlayer;
            Summon(player, "END_036");
            int before = player.HandZone.Count + player.DeckZone.Count;
            var source = Play(game, player, identity);
            while (player.Choice != null && game.Rewind?.Stage != SabberStoneCore.Model.Rewind.RewindStage.Offered)
                PlayerDecisionProof.Answer(game, player.Choice.Choices.First());
            Check(game.Rewind?.KeepBoth == true && player.HandZone.Count + player.DeckZone.Count == before + 1, "#158 first Herald outcome kept");
            Answer(game, true);
            Check(game.Resolution.HasSuspendedEffect && player.Choice != null && player.HandZone.Count + player.DeckZone.Count == before + 1,
                "#158 Morchie repeat must wait for controller's second Discover");
            var futures = new List<string>();
            foreach (var next in new[] { game, game.CloneExact(history: true), PinnedPoolFixture.LoadSnapshot(game.SaveSnapshot()) })
            {
                var owner = next.CurrentPlayer;
                int prompts = 0;
                while (owner.Choice != null)
                {
                    Check(++prompts <= 3, "#158 bounded Dark Gift decisions");
                    var decision = next.GetPendingDecisions().Single();
                    Check(decision.ControllerId == owner.Id && owner.Options().All(t => t is ChooseTask), "#158 NPC sees normal choices");
                    int selected = owner.Choice.Choices.Last();
                    if (seat == 2)
                    {
                        int index = 0;
                        var searched = BotDriver.Decide(next, owner.PlayerId, DeckStyle.Value,
                            state => PilotDecision.From(BotSearch.Choose(state, new BotBudget(1, 3, 32, int.MaxValue))), ref index);
                        Check(searched.Trace.Kind == "search" && searched.Trace.PilotDecision.Nodes > 0, "#158 NPC searches second Discover: " + searched.Trace.FallbackReason);
                        selected = ((ChooseTask)searched.Task).Choices.Single();
                    }
                    PlayerDecisionProof.Answer(next, selected);
                }
                Check(owner.HandZone.Count + owner.DeckZone.Count == before + 2 && owner.NumCardsPlayedThisTurn == 1 && owner.UsedMana == source.Card.Cost,
                    "#158 keep both outcomes, one payment/play");
                Balanced(next);
                futures.Add(Canonical.State(next) + Canonical.Value(next.Random.CaptureState()));
            }
            Check(futures.Distinct().Count() == 1, "#158 suspended repeat load/clone parity");
        }
        Console.WriteLine("PASS #158 Morchie Herald: both seats, player chooses both Discovers/Dark Gifts, load/clone parity");
    }

    private static void MorchieDecisionSweep()
    {
        var inventory = Cards.All.Where(c => c.Implemented && c[GameTag.REWIND] == 1 &&
            c.Power?.PowerTask is DecisionBattlecryTask or DecisionSpellTask or SuspendableTask).Select(c => c.Id).Order().ToArray();
        Check(inventory.SequenceEqual(new[] { "CORE_EDR_004", "CORE_EDR_004_2026", "TIME_EVENT_999" }), "Rewind decision inventory must have sweep coverage");
        foreach (int seat in new[] { 1, 2 })
        foreach (string identity in new[] { "TIME_EVENT_999", "TIME_039", "TIME_770" })
        {
            var game = ReportFixture(seat); var player = game.CurrentPlayer;
            Summon(player, "END_036");
            Add(player.Opponent, "CS2_029"); Add(player.Opponent, "CS2_182");
            var source = Add(player, identity); source[GameTag.REWIND] = 1;
            PlayEntity(game, player, source);
            while (!RewindTestHelpers.IsRewindOffered(game))
            {
                Check(player.Choice != null, identity + " first decision");
                PlayerDecisionProof.Answer(game, player.Choice.Choices.First());
            }
            int plays = player.NumCardsPlayedThisTurn, mana = player.UsedMana, casts = player.NumSpellsPlayedThisGame;
            Answer(game, true);
            Check(game.Resolution.HasSuspendedEffect && player.Choice != null, identity + " repeat awaits decision");
            Check(player.Choice.ChoiceType == (identity == "TIME_770" ? ChoiceType.TARGET : ChoiceType.GENERAL), identity + " decision kind");
            var futures = new List<string>();
            foreach (var next in new[] { game, game.CloneExact(history: true), PinnedPoolFixture.LoadSnapshot(game.SaveSnapshot()) })
            {
                var owner = next.CurrentPlayer;
                int prompts = 0;
                while (owner.Choice != null)
                {
                    Check(++prompts < 5, "bounded repeat decisions");
                    var pending = next.GetPendingDecisions().Single();
                    Reject(next, new DecisionAnswer(pending.Id, owner.Opponent.Id, new[] { pending.Options.First().OptionId }));
                    PlayerDecisionProof.Answer(next, owner.Choice.Choices.Last());
                }
                Check(next.Rewind == null && owner.NumCardsPlayedThisTurn == plays && owner.UsedMana == mana && owner.NumSpellsPlayedThisGame == casts,
                    identity + " effect-only repeat adds no payment/play/cast counters");
                Check(next.PowerHistory.Full.All(e => e is not SabberStoneCore.Kettle.PowerHistoryResetGame), "Morchie emits no reset");
                Balanced(next);
                futures.Add(Canonical.State(next) + Canonical.Value(next.Random.CaptureState()));
            }
            Check(futures.Distinct().Count() == 1, identity + " repeat load/clone parity");
        }
        Console.WriteLine("PASS Morchie decision sweep: CORE_EDR_004, CORE_EDR_004_2026, TIME_EVENT_999; Rewind-tagged TIME_039 Discover and TIME_770 TARGET; both seats, load/clone, invalid owner, no extra play tails");
    }

    private static void BronzeAfterDeaths()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var game = ReportFixture(seat); var player = game.CurrentPlayer;
            new SabberStoneCore.Tasks.SimpleTasks.ReplaceHeroPower(Cards.FromId("END_000p")).Process(game, player, player.Hero, null);
            var eggs = new[] { Summon(player.Opponent, "DINO_410t3"), Summon(player.Opponent, "DINO_410t3"), Summon(player.Opponent, "DINO_410t4") };
            foreach (var egg in eggs) egg.ToBeDestroyed = true;
            game.DeathProcessingAndAuraUpdate();
            var diagnostics = new List<string>();
            game.RewindBoundaryDiagnostic = diagnostics.Add;
            Console.WriteLine($"INFO #159 after Egg deaths: summoned={game.SummonedMinions.Count}, dead={game.DeadMinions.Count}, queue={game.TaskQueue.IsQuiescent}, resolution={game.Resolution.IsQuiescent}");
            Check(game.Process(HeroPowerTask.Any(player)), "#159 Bronze activation accepted");
            Check(RewindTestHelpers.IsRewindOffered(game), "#159 Bronze must offer Rewind after deathrattle summons");
            // #207: the Egg Deathrattle summons resolve in their own cleanup, so the boundary is clean and nothing is settled.
            Check(game.SummonedMinions.Count == 0 && diagnostics.Count == 0, "#159 Deathrattle summons leave no settlement: " + string.Join("|", diagnostics));
            var sink = game.RewindBoundaryDiagnostic;
            Answer(game, true);
            Check(ReferenceEquals(sink, game.RewindBoundaryDiagnostic), "#159 native diagnostic sink survives Rewind restore");
            player = game.CurrentPlayer;
            Check(player.HandZone.Count == 1 && game.Rewind == null, "#159 retry retains one minion and exhausts Rewind");
            Balanced(game);
            End(game); End(game);
            player = game.CurrentPlayer;
            Check(game.Process(HeroPowerTask.Any(player)) && RewindTestHelpers.IsRewindOffered(game), "#159 first use next turn offers again");
            Answer(game, false);
        }
        Console.WriteLine("PASS #159 Bronze after three Egg deathrattles: both seats, first-use offer and retry");
        var busy = ReportFixture(1); var card = Add(busy.CurrentPlayer, "TIME_001");
        var warnings = new List<string>(); busy.RewindBoundaryDiagnostic = message => { warnings.Add(message); throw new Exception("broken sink"); };
        busy.TaskQueue.StartEvent();
        try
        {
            Check(!SabberStoneCore.Model.Rewind.RewindRules.TryBegin(busy, PlayCardTask.Any(busy.CurrentPlayer, card)) &&
                warnings.Single().StartsWith("Skipped") && warnings[0].Contains("queue=False"), "#159 busy skip diagnoses queue; broken observer cannot abort action");
        }
        finally { busy.TaskQueue.EndEvent(); }

        var pending = ReportFixture(1); var owner = pending.CurrentPlayer;
        var tidecaller = Summon(owner, "EX1_509");
        pending.PowerHistory.Last.Clear();
        int attack = tidecaller.AttackDamage;
        Generic.SummonBlock(pending, (Minion)Entity.FromCard(owner, Cards.FromId("EX1_506")), -1, null);
        var spell = Add(owner, "TIME_001");
        Check(pending.Process(PlayCardTask.Any(owner, spell)) && RewindTestHelpers.IsRewindOffered(pending), "#159 settle pending live summon reaction");
        Check(tidecaller.AttackDamage == attack + 1 && pending.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>()
            .Any(e => e.EntityId == tidecaller.Id && e.Tag == GameTag.ATK && e.Value == attack + 1), "#159 summon reaction history retained before Rewind action");
        var baseline = PinnedPoolFixture.LoadSnapshot(pending.Rewind.Baseline);
        Check(((Minion)baseline.IdEntityDic[tidecaller.Id]).AttackDamage == attack + 1 && baseline.SummonedMinions.Count == 0,
            "#159 baseline includes settled summon reaction");
        Answer(pending, true);
        Check(((Minion)pending.IdEntityDic[tidecaller.Id]).AttackDamage == attack + 1, "#159 retry does not repeat settled summon reaction");

        foreach (int seat in new[] { 1, 2 })
        foreach (bool autonomous in new[] { false, true })
        {
            var lethal = ReportFixture(seat); var victim = lethal.CurrentPlayer;
            Summon(victim, "CS2_237"); // Buzzard draws on the overdue Beast summon.
            foreach (var held in victim.HandZone.ToArray()) victim.SetasideZone.Add(victim.HandZone.Remove(held));
            foreach (var held in victim.DeckZone.ToArray()) victim.SetasideZone.Add(victim.DeckZone.Remove(held));
            var unpaid = Add(victim, "TIME_001"); victim.Hero.Health = 1;
            Generic.SummonBlock(lethal, (Minion)Entity.FromCard(victim, Cards.FromId("CS2_172")), -1, null);
            var unpaidTask = PlayCardTask.Any(victim, unpaid);
            bool terminalAccepted = autonomous ? lethal.ProcessAutonomous(unpaidTask) : lethal.Process(unpaidTask);
            Check(terminalAccepted && lethal.State == State.COMPLETE &&
                victim.UsedMana == 0 && unpaid.Zone == victim.HandZone && lethal.Rewind == null,
                $"#159 lethal pending summon ends match before payment or Rewind action: accepted={terminalAccepted}, state={lethal.State}, mana={victim.UsedMana}, zone={unpaid.Zone.Type}");
        }
        Console.WriteLine("PASS #159 boundary safety: skip diagnostics, observer isolation/restore, pending trigger history, lethal catch-up prevents payment");
    }

    private static void AnnouncerReports()
    {
        foreach (int seat in new[] { 1, 2 })
        foreach (bool morchie in new[] { false, true })
        foreach (bool rewind in new[] { false, true })
        {
            var game = ReportFixture(seat); var player = game.CurrentPlayer;
            if (morchie) Summon(player, "END_036");
            Play(game, player, "TIME_034");
            Answer(game, rewind);
            player = game.CurrentPlayer;
            Check(game.Rewind == null && player.Choice == null && player.Hero.Weapon != null && player.Opponent.Hero.Weapon != null,
                "#80 Announcer settles both weapons");
            Check(player.Options().Any(t => t is EndTurnTask), "#80 End Turn available");
            Balanced(game);
        }
        Console.WriteLine("PASS #80 Stadium Announcer: both seats, Keep/Rewind, with/without Morchie, no freeze");
    }

    private static void SettlementInvalidatesAction(string only)
    {
        foreach (string scenario in new[] { "hand", "target" }.Where(s => only == null || only == s))
        foreach (int seat in new[] { 1, 2 })
        foreach (bool autonomous in new[] { false, true })
        foreach (bool skipPrePhase in new[] { false, true })
        {
            var game = ReportFixture(seat); var player = game.CurrentPlayer;
            IPlayable source; Minion target = null;
            if (scenario == "hand")
            {
                new ReplaceHeroPower(Cards.FromId("END_000p")).Process(game, player, player.Hero, null);
                Summon(player, "CS2_237");
                while (player.HandZone.Count < 9) Add(player, "CS2_182");
                source = player.Hero.HeroPower;
            }
            else
            {
                target = Summon(player.Opponent, "CS2_231");
                Summon(player, "NEW1_019");
                source = Add(player, "CS2_029"); source[GameTag.REWIND] = 1;
            }
            game.SetRandomSeed(3);
            Generic.SummonBlock(game, (Minion)Entity.FromCard(player, Cards.FromId("CS2_172")), -1, null);
            var task = player.Options().First(t => (scenario == "hand" ? t is HeroPowerTask : t.Source?.Id == source.Id)
                && t.ActionTarget?.Id == target?.Id);
            task.SkipPrePhase = skipPrePhase;
            if (target != null) Check(target.Zone == player.Opponent.BoardZone, "F1 target still on board when action offered");
            int deck = player.DeckZone.Count;
            bool accepted = autonomous ? game.ProcessAutonomous(task) : game.Process(task);
            Check(accepted && game.Rewind == null && player.UsedMana == 0 && player.NumCardsPlayedThisTurn == 0 &&
                player.NumSpellsPlayedThisGame == 0 && player.NumTimesHeroPowerUsedThisGame == 0,
                $"F1 {scenario} handled no-op: seat={seat}, autonomous={autonomous}, accepted={accepted}, mana={player.UsedMana}");
            Check(scenario == "hand" ? player.HandZone.Count == 10 && player.DeckZone.Count == deck - 1 && !player.Hero.HeroPower.IsExhausted
                : target.Zone == player.Opponent.GraveyardZone && source.Zone == player.HandZone,
                "F1 settlement retained without playing stale task");
            Check(game.PowerHistory.Last.OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>().Any(e => scenario == "hand"
                ? e.Tag == GameTag.ZONE && e.Value == (int)Zone.HAND
                : e.EntityId == target.Id && e.Tag == GameTag.ZONE && e.Value == (int)Zone.GRAVEYARD), "F1 settlement history retained");
            Check(player.Options().Any(t => t is EndTurnTask), "F1 fresh options available");
            Balanced(game);
            Console.WriteLine($"PASS F1 {scenario}: seat={seat}, autonomous={autonomous}, skipPrePhase={skipPrePhase}, settled no-op, history retained");
        }
    }

    private static void SettlementGuards()
    {
        foreach (bool autonomous in new[] { false, true })
        {
            var game = ReportFixture(1); var player = game.CurrentPlayer;
            Summon(player, "CS2_237"); var source = Add(player, "TIME_001");
            Generic.SummonBlock(game, (Minion)Entity.FromCard(player, Cards.FromId("CS2_172")), -1, null);
            player.UsedMana = 10;
            var task = PlayCardTask.Any(player, source);
            string before = Canonical.State(game) + Canonical.Value(game.Random.CaptureState()) + string.Concat(game.PowerHistory.Last.Select(e => e.Print()));
            Check(!(autonomous ? game.ProcessAutonomous(task) : game.Process(task)) &&
                before == Canonical.State(game) + Canonical.Value(game.Random.CaptureState()) + string.Concat(game.PowerHistory.Last.Select(e => e.Print())),
                "F1 initially illegal Rewind play rejected without settlement or history mutation");
        }
        foreach (int seat in new[] { 1, 2 })
        foreach (string scenario in new[] { "board", "choice" })
        {
            var game = ReportFixture(seat); var player = game.CurrentPlayer;
            var source = Add(player, scenario == "board" ? "TIME_002" : "TIME_001");
            if (scenario == "board") for (int i = 0; i < 5; i++) Summon(player, "CS2_231");
            game.TriggerManager.Events.Subscribe<SummonEvent>(_ => {
                if (scenario == "board")
                {
                    if (!player.BoardZone.IsFull) Generic.SummonBlock(game, (Minion)Entity.FromCard(player, Cards.FromId("CS2_231")), -1, null);
                }
                else if (player.Choice == null)
                    Generic.CreateChoiceCards(player, source, null, ChoiceType.GENERAL, ChoiceAction.HAND, new[] { Cards.FromId("CS2_182") }, null);
            });
            Generic.SummonBlock(game, (Minion)Entity.FromCard(player, Cards.FromId("CS2_231")), -1, null);
            var task = player.Options().First(t => t.Source?.Id == source.Id);
            Check(game.Process(task) && game.Rewind == null && source.Zone == player.HandZone && player.UsedMana == 0,
                "F1 " + scenario + " cancels stale action");
            Check(scenario == "board" ? player.BoardZone.IsFull : player.Choice != null && player.Options().All(t => t is ChooseTask),
                "F1 settlement preserves full board or legacy choice");
        }
        var capped = ReportFixture(1); var owner = capped.CurrentPlayer;
        var card = Add(owner, "TIME_001");
        int deaths = 0;
        capped.TriggerManager.Events.Subscribe<SummonEvent>(sender => ((Minion)sender).ToBeDestroyed = true);
        capped.TriggerManager.Events.Subscribe<DeathEvent>(_ => {
            deaths++;
            Generic.SummonBlock(capped, (Minion)Entity.FromCard(owner, Cards.FromId("CS2_231")), -1, null);
        });
        Generic.SummonBlock(capped, (Minion)Entity.FromCard(owner, Cards.FromId("CS2_231")), -1, null);
        var warnings = new List<string>(); capped.RewindBoundaryDiagnostic = warnings.Add;
        var limits = new List<string>(); capped.CycleBudgetExhaustedDiagnostic = limits.Add;
        // The settlement's first cleanup resolves the cycle itself; the shared cycle budget ends the game in a draw.
        Check(!SabberStoneCore.Model.Rewind.RewindRules.TryBegin(capped, PlayCardTask.Any(owner, card)) && deaths == Game.MaxCleanupCycles / 2 &&
            capped.State == State.COMPLETE && owner.PlayState == PlayState.TIED && owner.Opponent.PlayState == PlayState.TIED &&
            limits.Count == 1 && warnings.Count(w => w.StartsWith("Settlement limit=")) == 0 && capped.SummonedMinions.Count == 0 && capped.Rewind == null,
            "F5 pathological summon/death settlement stops at the cleanup cycle budget in a draw");
        Console.WriteLine("PASS F1/F5 boundary guards: both seats, full board, legacy choice; summon/death cycle ends in a draw at the cleanup cycle budget");
    }

    internal static void ReportRegressions(string only = null)
    {
        if (only == null || only.StartsWith("F1")) SettlementInvalidatesAction(only?.Split('-').ElementAtOrDefault(1));
        if (only == null || only == "guards") SettlementGuards();
        if (only == null || only == "158") MorchieHerald();
        if (only == null || only == "159") BronzeAfterDeaths();
        if (only == null || only == "80") AnnouncerReports();
        if (only == null || only == "sweep") MorchieDecisionSweep();
    }
}
