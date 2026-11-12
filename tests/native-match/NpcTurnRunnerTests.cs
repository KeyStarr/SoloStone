using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Config;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class NpcTurnRunnerTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new Exception("NpcTurnRunner: " + message); }

    public static void Run()
    {
        FastFinishNativeClockCut();
        ThinkingHoverFrames();
        NativePublicInputs();
        HostFailuresAndChoices();
        NativePaths();
        RealGoldenMatches();
        Console.WriteLine("PASS NpcTurnRunner scheduling, native parity, recording, cancellation, timer and legacy isolation");
    }

    // Native host, real golden pilot: a clock that blows the 55 s wall at once reproduces the incident's clock-cut fallback
    // (planNodeCap=0, no fallback node). Ready minions must still attack (fastFinish=attack), never END_TURN with them
    // idle. With a clock that does not run out the same position never enters fast finish.
    private static void FastFinishNativeClockCut()
    {
        var previous = NpcPilots.Factory;
        try
        {
            NpcPilots.Factory = null;
            foreach (bool exhausted in new[] { true, false })
            {
                var rig = new NativeRig(NpcCatalog.All[0].DeckId, false, clockStep: exhausted ? 5000 : 0);
                var game = rig.Match.Game; var bot = game.Player2;
                foreach (var id in new[] { "CS2_120", "CS2_182" })
                {
                    var minion = (SabberStoneCore.Model.Entities.Minion)SabberStoneCore.Model.Entities.Entity.FromCard(bot, Cards.FromId(id), zone: bot.BoardZone);
                    minion.IsExhausted = false;
                }
                game.SummonedMinions.Clear();
                int before = game.Player1.Hero.Health + game.Player1.Hero.Armor;
                rig.EndHumanTurn();
                var lines = rig.Log.Where(l => l.Contains(" action=") && l.Contains(" fastFinish=")).ToList();
                if (exhausted)
                    Check(lines.Any(l => l.Contains(" fastFinish=attack")) && game.Player1.Hero.Health + game.Player1.Hero.Armor <= before - 6,
                        "exhausted wall: ready minions must attack before END_TURN: " + string.Join("|", lines));
                else
                    Check(lines.Count > 0 && lines.All(l => l.Contains(" fastFinish=- replan=")), "normal budget entered fast finish: " + string.Join("|", lines));
                rig.Match.StopNpc();
            }
        }
        finally { NpcPilots.Factory = previous; }
    }

    private static void NativePublicInputs()
    {
        var previous = NpcPilots.Factory;
        try
        {
            NpcPilots.Factory = null;
            var rig = new NativeRig(NpcCatalog.All[0].DeckId, false);
            var game = rig.Match.Game; var human = game.Player1; var bot = game.Player2;
            human.BaseMana = 10; human.UsedMana = 0;
            var card = human.DeckZone.First(c => c.Card.Type == CardType.MINION);
            human.DeckZone.Remove(card); human.HandZone.Add(card);
            Check(game.Process(human.Options().First(t => t.Source?.Id == card.Id)), "public play fixture failed");
            rig.EndHumanTurn(false);
            var context = new GoldenContext(game.Turn, 1, 7, NpcCatalog.All[0]);
            var job = rig.Match.BeginNpcDecision(context) as GoldenDecisionJob;
            Check(job != null && job.PublicInputs.OpponentPlayed.Contains(card.Card.Id), "native host omitted public opponent plays");

            var source = SabberStoneCore.Model.Entities.Entity.FromCard(bot, Cards.FromId("EDR_456"), zone: bot.GraveyardZone);
            var one = SabberStoneCore.Model.Entities.Entity.FromCard(bot, Cards.FromId("CS2_231"), zone: bot.SetasideZone);
            var two = SabberStoneCore.Model.Entities.Entity.FromCard(bot, Cards.FromId("CS2_182"), zone: bot.SetasideZone);
            bot.Choice = new Choice(bot) { SourceId = source.Id, ChoiceType = ChoiceType.GENERAL,
                ChoiceAction = ChoiceAction.HAND, Choices = new List<int> { one.Id, two.Id } };
            job = rig.Match.BeginNpcDecision(context) as GoldenDecisionJob;
            Check(job != null && job.PublicInputs.DiscoverSource == "EDR_456"
                && job.PublicInputs.Offered.Select(p => p.Key).SequenceEqual(new[] { one.Id, two.Id }),
                "native host omitted discover source/offers");
            rig.Match.StopNpc();
        }
        finally { NpcPilots.Factory = previous; }
    }

    private static void ThinkingHoverFrames()
    {
        var previous = NpcPilots.Factory;
        try
        {
            NpcPilots.Factory = null;
            var rig = new NativeRig(NpcCatalog.All[0].DeckId, false, clockStep: .1, presentation: true);
            SabberStoneCore.Model.Entities.Entity.FromCard(rig.Match.Game.Player2, Cards.FromId("CS2_231"), zone: rig.Match.Game.Player2.HandZone);
            rig.EndHumanTurn(false); rig.Match.NpcRunner.SliceMilliseconds = 1; rig.Match.NpcRunner.IdleSliceMilliseconds = 1; rig.Match.NpcRunner.MaxIdleSliceMilliseconds = 1;
            bool thinkingHover = false;
            for (int i = 0; i < 10000 && rig.Record.Decisions == 0; i++)
            {
                rig.Now += 50; rig.Match.TickNpc();
                thinkingHover |= rig.Match.NpcRunner.State == NpcTurnState.Thinking
                    && rig.UserUi.Any(p => p.Value.HasMouseInfo && p.Value.MouseInfo.OverCard > 0);
            }
            Check(thinkingHover, "no real GoldenPilot hover during Thinking frames");
            int source = rig.Record.LastDecision.Action.Source;
            var hovers = rig.UserUi.Where(p => p.Value.HasMouseInfo).ToArray();
            Check(source > 0 && hovers.Last().Value.MouseInfo.OverCard == 0, "acted card hover not cleared");
            var focus = hovers.Last(p => p.Value.MouseInfo.OverCard == source);
            Check(hovers.Last().Key - focus.Key >= 300, "acted card lacked 300ms dwell");
            var changes = hovers.Where(p => p.Value.MouseInfo.OverCard > 0).ToArray();
            Check(changes.Zip(changes.Skip(1), (a, b) => b.Key - a.Key).All(ms => ms >= 600), "hover changed within 600ms");
            Check(rig.Match.NpcRunner.ArtificialDelayMilliseconds <= 4000, "hover exceeded artificial delay cap");

            var legacy = new NativeRig("mage", false, presentation: true);
            legacy.EndHumanTurn();
            Check(legacy.Match.NpcRunner == null && legacy.UserUi.Count == 0, "legacy opponent emitted NPC presentation");
        }
        finally { NpcPilots.Factory = previous; }
    }

    private static object Invoke(NativeMatch match, string name, params object[] args) =>
        typeof(NativeMatch).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, args);

    private static void HostFailuresAndChoices()
    {
        var previous = NpcPilots.Factory;
        try
        {
            NpcPilots.Factory = null;
            var secret = new NativeRig(NpcCatalog.All[0].DeckId, false);
            SabberStoneCore.Model.Entities.Entity.FromCard(secret.Match.Game.Player1, Cards.FromId("EX1_287"),
                zone: secret.Match.Game.Player1.SecretZone);
            secret.EndHumanTurn();
            Check(secret.Record.Decisions > 0 && secret.Match.BotFallbacks == 0, "human Secret forced live golden fallback");

            var invalid = new NativeRig(NpcCatalog.All[0].DeckId, false);
            invalid.EndHumanTurn(false);
            var illegal = new GoldenDecision(BotAction.From(ConcedeTask.Any(invalid.Match.Game.Player1)), 0, 0, 0, "Forced", null, null);
            int hashes = invalid.Record.Hashes.Count;
            Invoke(invalid.Match, "ActNpc", illegal);
            Check(invalid.Match.BotFallbacks == 1 && invalid.Record.Hashes.Count == hashes + 1,
                "unresolvable action did not process exactly one fallback");

            NpcPilots.Factory = p => new Pilot();
            var failed = new NativeRig(NpcCatalog.All[0].DeckId, false);
            failed.EndHumanTurn(false); failed.FailHistory = true;
            hashes = failed.Record.Hashes.Count;
            bool threw = false;
            for (int i = 0; i < 100 && !threw; i++)
            {
                failed.Now += 1000;
                try { failed.Match.TickNpc(); } catch (IOException) { threw = true; failed.Match.StopNpc(); }
            }
            for (int i = 0; i < 5; i++) failed.Match.TickNpc();
            Check(threw && failed.Record.Hashes.Count == hashes + 1 && failed.Record.Decisions == 1,
                "history failure replayed authoritative action");

            var interrupted = new NativeRig(NpcCatalog.All[0].DeckId, false, clockStep: .5);
            interrupted.EndHumanTurn(false);
            interrupted.Match.NpcRunner.SliceMilliseconds = 1; interrupted.Match.NpcRunner.IdleSliceMilliseconds = 1; interrupted.Match.NpcRunner.MaxIdleSliceMilliseconds = 1;
            interrupted.Now += 2000; interrupted.Match.TickNpc(); interrupted.Match.TickNpc();
            var runner = interrupted.Match.NpcRunner;
            double search = runner.SearchMilliseconds, delay = runner.ArtificialDelayMilliseconds;
            var human = interrupted.Match.Game.Player1;
            var offer = SabberStoneCore.Model.Entities.Entity.FromCard(human, Cards.FromId("CS2_231"), zone: human.SetasideZone);
            human.Choice = new Choice(human) { ChoiceType = ChoiceType.GENERAL, ChoiceAction = ChoiceAction.HAND,
                Choices = new List<int> { offer.Id }, SourceId = human.Hero.Id };
            interrupted.Match.TickNpc();
            Check(!runner.Active && interrupted.choices != null, "human choice did not interrupt NPC");
            interrupted.Match.SendChoices(interrupted.choices.Id, new List<int> { offer.Id });
            Check(ReferenceEquals(runner, interrupted.Match.NpcRunner) && runner.State == NpcTurnState.Thinking
                && runner.SearchMilliseconds == search && runner.ArtificialDelayMilliseconds == delay, "human answer reset turn budgets");
            runner.Cancel(); Invoke(interrupted.Match, "DriveUntilHumanInput");
            Check(ReferenceEquals(runner, interrupted.Match.NpcRunner) && runner.State == NpcTurnState.Thinking
                && runner.ArtificialDelayMilliseconds == delay, "cancelled native runner restarted pre-turn pause");

            NpcPilots.Factory = p => new UnslicedPilot();
            var legacy = new NativeRig(NpcCatalog.All[0].DeckId, false);
            legacy.EndHumanTurn();
            Check(legacy.Match.NpcRunner == null && legacy.Log.Count(x => x.Contains("not frame-sliced")) == 1
                && legacy.Match.Game.CurrentPlayer == legacy.Match.Game.Player1, "non-sliced pilot not routed to legacy search");

            NpcPilots.Factory = null;
            foreach (string cardId in new[] { "EDR_856", "TIME_034" })
            {
                var choice = new NativeRig(NpcCatalog.All[0].DeckId, false);
                choice.EndHumanTurn(false);
                var game = choice.Match.Game; var bot = game.Player2;
                bot.BaseMana = 10; bot.UsedMana = 0;
                var card = SabberStoneCore.Actions.Generic.DrawCard(bot, Cards.FromId(cardId));
                Invoke(choice.Match, "Process", bot.Options().First(t => t.Source?.Id == card.Id));
                Check(bot.Choice != null, "choice fixture missing " + cardId);
                bool rewind = cardId == "TIME_034";
                BotAction expected = null;
                if (rewind)
                {
                    bot.Hero.AddWeapon((SabberStoneCore.Model.Entities.Weapon)SabberStoneCore.Model.Entities.Entity.FromCard(bot, Cards.FromId("BE_227")));
                    game.Player1.Hero.AddWeapon((SabberStoneCore.Model.Entities.Weapon)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("CORE_RLK_086")));
                    expected = BotAction.From(ChooseTask.Pick(bot, game.Rewind.RewindEntityId));
                }
                for (int i = 0; choice.Record.Decisions == 0 && i < 1000; i++) { choice.Now += 100; choice.Match.TickNpc(); }
                Check(choice.Record.Decisions == 1 && choice.Match.BotFallbacks == 0, "NPC choice fell back " + cardId);
                if (rewind) Check(choice.Record.LastDecision.Action.Equals(expected), "live Rewind ignored pilot hook");
            }

            NpcPilots.Factory = p => new Pilot();
            var budget = new NativeRig(NpcCatalog.All[0].DeckId, false, turnNodes: 1);
            budget.EndHumanTurn();
            Check(budget.Log.Any(x => x.Contains("fallback=turn-nodes")), "playtest node override not consumed");
        }
        finally { NpcPilots.Factory = previous; }
    }

    private sealed class UnslicedPilot : IGoldenPilot
    {
        public GoldenDecision Decide(FairBotState view, GoldenContext context) { throw new Exception("unsliced pilot called"); }
        public IReadOnlyList<int> Mulligan(IGoldenMulliganView view, GoldenContext context) => new int[0];
    }

    private static void RealGoldenMatches()
    {
        var previous = NpcPilots.Factory;
        try
        {
            NpcPilots.Factory = null;
            foreach (var profile in NpcCatalog.All)
            {
                Check(NpcPilots.Create(profile) is GoldenPilot, "default catalog pilot is not GoldenPilot");
                var sync = new NativeRig(profile.DeckId, true);
                var sliced = new NativeRig(profile.DeckId, false, clockStep: .02);
                for (int turn = 0; turn < 90 && sync.Match.Game.State != State.COMPLETE; turn++) sync.EndHumanTurn();
                for (int turn = 0; turn < 90 && sliced.Match.Game.State != State.COMPLETE; turn++) sliced.EndHumanTurn();
                Check(sync.Match.Game.State == State.COMPLETE && sliced.Match.Game.State == State.COMPLETE,
                    profile.Id + " real golden match unfinished");
                Check(sync.Record.Actions.Count > 0 && sync.Record.Actions.SequenceEqual(sliced.Record.Actions),
                    profile.Id + " real golden sync/sliced task sequence differs");
                Check(sync.Record.Hashes.SequenceEqual(sliced.Record.Hashes), profile.Id + " real golden fingerprints differ");
                Check(!sliced.Log.Any(x => x.Contains("clock-cut") || x.Contains("unregistered")), "real golden fell back");
            }
            var cancelled = new NativeRig(NpcCatalog.All[0].DeckId, false, clockStep: .5);
            cancelled.EndHumanTurn(false);
            cancelled.Match.NpcRunner.SliceMilliseconds = 1; cancelled.Match.NpcRunner.IdleSliceMilliseconds = 1; cancelled.Match.NpcRunner.MaxIdleSliceMilliseconds = 1;
            cancelled.Now += 2000; cancelled.Match.TickNpc(); cancelled.Match.TickNpc();
            Check(cancelled.Match.NpcRunner.State == NpcTurnState.Thinking, "real job did not yield mid-search");
            int decisions = cancelled.Record.Decisions, packets = cancelled.History.Count;
            cancelled.Match.StopNpc();
            for (int i = 0; i < 10; i++) cancelled.Match.TickNpc();
            Check(cancelled.Match.NpcRunner.State == NpcTurnState.Cancelled
                && cancelled.Record.Decisions == decisions && cancelled.History.Count == packets, "cancelled real job acted");
        }
        finally { NpcPilots.Factory = previous; }
    }

    private static void NativePaths()
    {
        var previous = NpcPilots.Factory;
        try
        {
            NpcPilots.Factory = p => new Pilot();
            foreach (var profile in NpcCatalog.All)
            {
                var sync = new NativeRig(profile.DeckId, true);
                var sliced = new NativeRig(profile.DeckId, false);
                for (int turn = 0; turn < 3; turn++) { sync.EndHumanTurn(); sliced.EndHumanTurn(); }
                Check(sync.Record.Actions.SequenceEqual(sliced.Record.Actions), "decisions differ before history");
                Check(sync.Record.Hashes.SequenceEqual(sliced.Record.Hashes), "hashes differ before history");
                Check(sync.History.SequenceEqual(sliced.History), profile.Id + " native history differs");
                Check(sync.Record.Hashes.SequenceEqual(sliced.Record.Hashes), "fingerprints differ");
                Check(sync.Record.Actions.SequenceEqual(sliced.Record.Actions), "decisions differ");
                Check(sync.Record.Hashes.Count == sync.Record.HumanActions + sync.Record.Decisions + 2,
                    "not exactly one fingerprint per action (including bot mulligan and MainBegin)");
                Check(sliced.Match.NpcRunner != null && sliced.Match.Game.CurrentPlayer == sliced.Match.Game.Player1, "NPC did not return input");
                Check(sliced.Log.Any(x => x.Contains("npc=") && x.Contains("reuse=") && x.Contains("lethal=") && x.Contains("slice=")), "log fields missing");
            }
            NativeRig concede = null;
            NpcPilots.Factory = p => new Pilot(() => concede.Now += 3);
            concede = new NativeRig(NpcCatalog.All[0].DeckId, false);
            concede.EndHumanTurn(false);
            concede.Match.NpcRunner.IdleSliceMilliseconds = concede.Match.NpcRunner.MaxIdleSliceMilliseconds = 10;
            concede.Now += 2000; concede.Match.TickNpc(); concede.Match.TickNpc();
            Check(concede.Match.NpcRunner.State == NpcTurnState.Thinking, "fixture not thinking");
            concede.Match.Concede();
            int packets = concede.History.Count;
            for (int i = 0; i < 20; i++) concede.Match.TickNpc();
            Check(concede.Match.NpcRunner.State == NpcTurnState.Cancelled && concede.History.Count == packets,
                "concession left stale job/history");
            Check(concede.Record.Results == 1 && concede.Record.Concessions == 1, "concession/result repeated");
            string folder = Path.Combine(Path.GetTempPath(), "npc-settlement-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string path = Path.Combine(folder, "profile.xml");
                using (var profile = new LocalProfile(path))
                {
                    var pending = profile.Begin("mage", NpcCatalog.All[0].DeckId, 8427);
                    profile.Settle(pending.Id, "Lost", concede.Match.Game.Turn, true, concede.Match.RewardFacts);
                    var bytes = File.ReadAllBytes(path);
                    profile.Settle(pending.Id, "Lost", concede.Match.Game.Turn, true, concede.Match.RewardFacts);
                    Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "concession settled twice");
                }
            }
            finally { Directory.Delete(folder, true); }
            NpcPilots.Factory = p => new Pilot();
            var complete = new NativeRig(NpcCatalog.All[0].DeckId, false);
            complete.EndHumanTurn(false);
            complete.Match.Game.Process(ConcedeTask.Any(complete.Match.Game.Player2));
            complete.Match.TickNpc();
            Check(!complete.Match.NpcRunner.Active && complete.Record.Decisions == 0, "completion did not stop work");
            var stop = new NativeRig(NpcCatalog.All[0].DeckId, false);
            stop.EndHumanTurn(false); stop.Match.StopNpc(); stop.Match.TickNpc();
            Check(stop.Match.NpcRunner.State == NpcTurnState.Cancelled && stop.Record.Decisions == 0, "stop did not cancel");
            var timer = new NativeRig(NpcCatalog.All[0].DeckId, false);
            timer.Match.Game.Player2[GameTag.TIMEOUT] = 15;
            timer.EndHumanTurn(false);
            timer.Match.TickTurnTimer(long.MaxValue);
            Check(timer.Match.Game.CurrentPlayer == timer.Match.Game.Player1 && !timer.Match.NpcRunner.Active, "timer failed to end NPC turn");
            Check(timer.Record.Decisions == 1 && timer.Record.Hashes.Count == timer.Record.HumanActions + timer.Record.Decisions + 2,
                "timeout action missing recorder decision/fingerprint");
            var legacy = new NativeRig("mage", false); var legacySync = new NativeRig("mage", true);
            legacy.EndHumanTurn(); legacySync.EndHumanTurn();
            Check(legacy.Match.NpcRunner == null && legacySync.Match.NpcRunner == null, "legacy created runner");
            // Historical legacy behavior is pinned by ShowcasePlayProof's table, checked before/after this change.
            int hashes = 0;
            var disabled = new NativeRig(NpcCatalog.All[0].DeckId, true, false);
            disabled.Match.ReplayFingerprint = g => { hashes++; throw new Exception("disabled hash invoked"); };
            disabled.EndHumanTurn();
            Check(hashes == 0, "null recorder computed hash");
            var failing = new NativeRig(NpcCatalog.All[0].DeckId, true);
            failing.Match.ReplayFingerprint = g => { throw new IOException("full disk"); };
            failing.EndHumanTurn();
            Check(failing.Match.Game.CurrentPlayer == failing.Match.Game.Player1, "recorder failure affected match");
            NpcPilots.Factory = null;
            Check(NpcPilots.Create(NpcCatalog.All[0]) is IFrameSlicedGoldenPilot, "missing registered pilot fallback");
        }
        finally { NpcPilots.Factory = previous; }
    }

    private sealed class Pilot : IFrameSlicedGoldenPilot
    {
        private readonly Action step;
        internal Pilot(Action step = null) { this.step = step; }
        public IGoldenDecisionJob Begin(FairBotState view, GoldenContext context) => new PilotJob(view, step);
        public GoldenDecision Decide(FairBotState view, GoldenContext context)
        { var job = Begin(view, context); while (!job.IsDone) job.StepNode(); return job.Result; }
        public IReadOnlyList<int> Mulligan(IGoldenMulliganView view, GoldenContext context) => new int[0];
    }
    private sealed class PilotJob : IGoldenDecisionJob
    {
        private readonly FairBotState view;
        private int nodes;
        private readonly Action step;
        internal PilotJob(FairBotState view, Action step) { this.view = view; this.step = step; }
        public bool IsDone => nodes == 7;
        public void StepNode() { nodes++; step?.Invoke(); }
        public GoldenDecision Result => new GoldenDecision(view.OrderedActions[0], 1, nodes, 1, "Nodes", null, null);
        public string LethalResult => "none";
        public bool PlanReused => false;
    }
    private sealed class Recording : IMatchRecorder
    {
        public bool Enabled => true;
        internal readonly List<string> Hashes = new List<string>(), Actions = new List<string>();
        internal int HumanActions, Decisions, Results, Concessions;
        internal GoldenDecision LastDecision;
        public void OnMulligan(int playerId, IReadOnlyList<int> kept) { }
        public void OnDecision(GoldenContext context, GoldenDecision trace, string lethal, bool reuse, int nodes, double ms, double slice)
        { Decisions++; LastDecision = trace; Actions.Add(trace.Action.ToString()); }
        public void OnHumanAction(int turn, string action, int index, IReadOnlyList<int> choices, bool conceded) { HumanActions++; }
        public void OnEmote(int playerId, string emote, int eventIndex) { }
        public void OnConcession(int playerId, int turn) { Concessions++; }
        public void OnResult(string result, int turns) { Results++; }
        public void OnPostAction(long sequence, string hash)
        { Check(sequence == Hashes.Count + 1, "fingerprint sequence gap"); Hashes.Add(hash); }
    }
    private sealed class NativeRig
    {
        internal double Now;
        internal readonly NativeMatch Match;
        internal readonly Recording Record = new Recording();
        internal readonly List<string> History = new List<string>(), Log = new List<string>();
        internal readonly List<KeyValuePair<double, UserUI>> UserUi = new List<KeyValuePair<double, UserUI>>();
        internal EntityChoices choices;
        internal bool FailHistory;
        private AllOptions options;
        internal NativeRig(string deck, bool synchronous, bool record = true, double clockStep = 0, int? turnNodes = null, bool presentation = false)
        {
            Match = new NativeMatch((id, packet) => {
                if (id == 19 && FailHistory) throw new IOException("history send failed");
                if (id == 17) choices = (EntityChoices)packet;
                if (id == 14) options = (AllOptions)packet;
                using (var bytes = new MemoryStream()) { packet.Serialize(bytes); if (id == 19) History.Add(Convert.ToBase64String(bytes.ToArray())); }
            }, "mage", deck, 8427) { NpcTurnNodes = turnNodes, SynchronousNpc = synchronous, MatchId = "fixed-match", NpcClock = () => { Now += clockStep; return Now; }, NativeNpcIdle = () => true };
            Match.PresentationSettings = new NpcPresentationSettings { Emotes = presentation, Hover = presentation,
                Skin = presentation, CardBack = presentation };
            Match.NativeNpcStartingTaunt = () => false;
            Match.NativeNpcUserUI = packet => { UserUi.Add(new KeyValuePair<double, UserUI>(Now, packet)); return true; };
            if (record) Match.Recorder = Record;
            Match.BotTrace = Log.Add;
            Match.Start(); Match.SendChoices(choices.Id, new List<int>());
        }
        internal void EndHumanTurn(bool pump = true)
        {
            int index = options.Options.FindIndex(o => o.Type_ == PegasusGame.Option.Type.END_TURN);
            Match.SendOption(options.Id, index, 0, -1, 0);
            if (!pump) return;
            for (int i = 0; Match.NpcRunner?.Active == true && i < 10000; i++) { Now += 100; Match.TickNpc(); }
            Check(Match.NpcRunner?.Active != true, "native runner stuck");
        }
    }

    private sealed class Job : IGoldenDecisionJob
    {
        private readonly Rig rig;
        private int nodes;
        internal Job(Rig rig) { this.rig = rig; }
        public bool IsDone => nodes == 7;
        public GoldenDecision Result => new GoldenDecision(rig.Action, 1, nodes, 1, "Nodes", null, null);
        public string LethalResult => "none";
        public bool PlanReused => false;
        public void StepNode() { nodes++; rig.Steps++; rig.Now += rig.NodeCost; if (rig.ThrowSearch) { rig.ThrowSearch = false; throw new InvalidOperationException("injected-search"); } if (rig.ThrowWrapped) { rig.ThrowWrapped = false; throw new SimulationFailureException(rig.Action, new InvalidOperationException("injected-original")); } }
    }

    private sealed class Rig
    {
        internal double Now, NodeCost = 3;
        internal int Steps;
        internal bool Idle = true, ThrowAct, ThrowFallback, ThrowSearch, ThrowWrapped, SpendFallback;
        internal string Busy;
        internal readonly List<string> Actions = new List<string>(), Fallbacks = new List<string>(), Log = new List<string>();
        internal readonly NpcTurnRunner Runner;
        internal readonly BotAction Action;
        internal Rig(NpcProfile profile, int? turnNodes = null)
        {
            var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE });
            Action = BotAction.From(EndTurnTask.Any(game.Player2));
            Runner = new NpcTurnRunner(profile, "same-match", () => Now, () => Actions.Count < 30,
                () => new GoldenContext(2, Actions.Count + 1, 997 * 2 + (Actions.Count + 1) * 31, profile),
                ctx => new Job(this), reason => { Fallbacks.Add(reason); if (SpendFallback) Check(Runner.SpendFallbackNode(), "fallback reservation unavailable"); if (ThrowFallback) throw new IOException("fallback failed"); return new GoldenDecision(Action, 0, 0, 0, "Forced", reason, null, "fallback"); },
                decision => { Actions.Add(decision.Action.ToString()); if (ThrowAct) throw new IOException("send failed"); }, () => Idle, Log.Add, turnNodes: turnNodes,
                busyReason: () => Busy);
            Runner.Start();
            Check(Runner.ArtificialDelayMilliseconds >= profile.Personality.PreTurnMinSeconds * 1000
                && Runner.ArtificialDelayMilliseconds <= profile.Personality.PreTurnMaxSeconds * 1000, "wrong profile pacing");
        }
        internal void Run(bool sync, double frame = 10)
        {
            if (sync) Runner.RunSynchronously();
            else for (int i = 0; Runner.Active && i < 10000; i++) { Now += frame; Runner.Tick(); }
            Check(!Runner.Active, "runner did not finish");
        }
    }
}
