using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Config;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class HistoryHandDisplayProof
{
    private static Card DamageCard, HeldCard, ChoiceCard;
    private static long RecipeEntries, RecipeCalls;
    private static void Verify(bool condition, string message) => Core2026Proof.Check(condition, "history hand display: " + message);
    private static int Damage(IPlayable entity)
    {
        Verify(entity.Zone == entity.Controller.HandZone, "recipe evaluated outside owner's hand");
        var q = entity.Game.HistoryFacts.Query;
        int value = 1 + q.FriendlyDeathCount(entity.Controller.Id);
        RecipeEntries += q.EntriesExamined; RecipeCalls++;
        return value;
    }
    private static int Held(IPlayable entity)
    {
        var q = entity.Game.HistoryFacts.Query;
        int value = q.OwnerTurnsStartedWhileHolding(entity.Id, entity.Controller.Id);
        RecipeEntries += q.EntriesExamined; RecipeCalls++;
        return value;
    }
    private static int Left(IPlayable entity) => Math.Max(0, 3 - Held(entity));
    private static int Cost(IPlayable entity) => 1;

    internal static void Register()
    {
        if (DamageCard != null) return;
        var data = typeof(Cards).GetField("Data", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var cards = (Dictionary<string, Card>)data.GetType().GetProperty("Cards", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(data);
        Card Install(string source, string id, int dbf)
        {
            var card = Cards.FromId(source).Clone();
            typeof(Card).GetProperty("Id").SetValue(card, id);
            typeof(Card).GetField("<AssetId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(card, dbf);
            cards.Add(id, card);
            return card;
        }
        DamageCard = Install("CS2_029", "HISTORY_PROOF_HAND_DAMAGE", -23001);
        DamageCard.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Damage);
        DamageCard.Power = new Power { Aura = new AdaptiveCostEffect(Cost), PowerTask = DamageCard.Power.PowerTask };
        HeldCard = Install("GAME_005", "HISTORY_PROOF_HAND_HELD", -23002);
        HeldCard.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Held);
        HeldCard.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_2, Left);
        ChoiceCard = Install("GAME_005", "HISTORY_PROOF_HAND_CHOICE", -23003);
        ChoiceCard.Power = new Power { PowerTask = StateTaskList.Suspendable("proof/history-hand-choice/v1",
            new DiscoverThenContinueTask("CS2_231", "CS2_182"), new DrawDisplays(),
            new DiscoverThenContinueTask("CS2_231", "CS2_182")) };

    }

    internal static void Run()
    {
        Register();
        var failures = new List<Exception>();
        foreach (var proof in new Action[] { InitialDecisions, TradeDecision, ChainedDecision })
            try { proof(); } catch (Exception e) { Console.WriteLine("FAIL " + proof.Method.Name + ": " + e.Message); failures.Add(e); }
        if (failures.Count != 0) throw new AggregateException(failures);
        Validation();
        ZonesAndUpdates();
        Holding();
        Persistence();
        Privacy();
        SearchBound();
        NoDisplayNeutrality();
        Console.WriteLine("PASS history hand display: hand-only, death/turn/residence values, cost coexistence, stateless recipes, clone/snapshot/child, native packets + fair hidden permutations");
    }

    private sealed class DrawDisplays : SimpleTask
    {
        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null)
        {
            Generic.DrawCard(controller, DamageCard);
            Generic.DrawCard(controller, HeldCard);
            return TaskState.COMPLETE;
        }
    }

    private static void InitialDecisions()
    {
        foreach (bool history in new[] { false, true })
        foreach (bool skipMulligan in new[] { false, true })
        {
            var deck = Enumerable.Range(0, 30).Select(i => i % 2 == 0 ? DamageCard : HeldCard).ToList();
            var g = new Game(new GameConfig { RandomSeed = 73, StartPlayer = 1, Shuffle = false,
                SkipMulligan = skipMulligan, History = history, Logging = false, FormatType = FormatType.FT_WILD,
                Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE,
                Player1Deck = deck, Player2Deck = deck.ToList() });
            void AssertDisplays(bool firstTurnStarted)
            {
                foreach (var p in new[] { g.Player1, g.Player2 })
                {
                    var damage = p.HandZone.First(c => c.Card.Id == DamageCard.Id);
                    var held = p.HandZone.First(c => c.Card.Id == HeldCard.Id);
                    int turns = firstTurnStarted && p == g.FirstPlayer ? 1 : 0;
                    Verify(damage[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1
                        && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == turns
                        && held[GameTag.TAG_SCRIPT_DATA_NUM_2] == 3 - turns,
                        $"initial decision displays: skip={skipMulligan}, player={p.PlayerId}, " +
                        $"damage={damage[GameTag.TAG_SCRIPT_DATA_NUM_1]}, held={held[GameTag.TAG_SCRIPT_DATA_NUM_1]}, left={held[GameTag.TAG_SCRIPT_DATA_NUM_2]}");
                }
            }
            g.StartGame();
            AssertDisplays(skipMulligan); // Before any decision read, answer, or manual refresh.
            var decisions = g.GetPendingDecisions();
            Verify(skipMulligan ? decisions.Single().Kind == DecisionKind.Options
                : decisions.Count == 2 && decisions.All(d => d.Kind == DecisionKind.Mulligan), "initial decisions exposed");
            if (skipMulligan) continue;
            foreach (var p in new[] { g.Player1, g.Player2 })
            {
                var decision = g.GetPendingDecisions().Single(d => d.ControllerId == p.Id);
                Verify(g.TryAnswer(new DecisionAnswer(decision.Id, p.Id, Array.Empty<int>()), out var error), error);
                AssertDisplays(false); // Replacements refresh while the other mulligan can still be pending.
            }
            g.MainBegin(); // Same post-mulligan join used by the host.
            AssertDisplays(true);
            Verify(g.GetPendingDecisions().Single().Kind == DecisionKind.Options, "post-mulligan first turn exposed");
        }
        Console.WriteLine("PASS history display initial decisions: StartGame mulligan, replacements, first turn + SkipMulligan; no manual refresh");
    }

    private static void TradeDecision()
    {
        foreach (bool history in new[] { false, true })
        {
            var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
            Kill(g, Summon(p, "CS2_231"));
            var display = Entity.FromCard(p, DamageCard);
            p.DeckZone.Add(display);
            var trade = Add(p, "CORE_EX1_002");
            Verify(p.DeckZone.TopCard == display && TradeCardTask.IsValid(p, trade), "legal trade fixture");
            Verify(g.Process(TradeCardTask.Any(p, trade)), "trade accepted");
            Verify(display.Zone == p.HandZone && display[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
                "Trade draw refreshed before next decision without AuraUpdate");
            Verify(g.GetPendingDecisions().Single().Kind == DecisionKind.Options, "Trade returns options");
        }
        Console.WriteLine("PASS history display Trade: legal draw refreshed without manual AuraUpdate");
    }

    private static void ChainedDecision()
    {
        foreach (bool history in new[] { false, true })
        {
            var g = HistoryFactsProof.Fixture(history); var p = g.Player1;
            Kill(g, Summon(p, "CS2_231"));
            var source = Entity.FromCard(p, ChoiceCard, zone: p.HandZone);
            Verify(g.Process(PlayCardTask.Any(p, source)), "choice spell accepted");
            var decision = g.GetPendingDecisions().Single();
            Verify(g.TryAnswer(new DecisionAnswer(decision.Id, p.Id, new[] { decision.Options[0].OptionId }), out var error), error);
            Verify(g.Resolution.HasSuspendedEffect && g.TaskQueue.IsQuiescent, "second prompt skips cleanup path");
            Verify(p.HandZone.Single(c => c.Card.Id == DamageCard.Id)[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2
                && p.HandZone.Single(c => c.Card.Id == HeldCard.Id)[GameTag.TAG_SCRIPT_DATA_NUM_2] == 3,
                "chained choice refreshes new hand cards without AuraUpdate");
            var bytes = g.SaveSnapshot();
            foreach (var copy in new[] { g, g.CloneExact(), Game.LoadSnapshot(bytes) })
            {
                var next = copy.GetPendingDecisions().Single();
                Verify(copy.TryAnswer(new DecisionAnswer(next.Id, p.Id, new[] { next.Options[0].OptionId }), out error), error);
                Verify(!copy.Resolution.HasSuspendedEffect
                    && copy.Player1.HandZone.Single(c => c.Card.Id == DamageCard.Id)[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
                    "chained final answer survives clone/snapshot");
            }
        }
        Console.WriteLine("PASS history display chained choices: suspended draw refreshed without manual AuraUpdate, clone/snapshot");
    }

    private static void Validation()
    {
        void Reject(Action action, string name)
        {
            bool rejected = false;
            try { action(); } catch (ArgumentException) { rejected = true; }
            Verify(rejected, name);
        }
        var card = Cards.FromId("GAME_005").Clone();
        Reject(() => card.HistoryDisplay(GameTag.CONTROLLER, Damage), "public/behavioral tags rejected");
        Reject(() => card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, null), "null recipe rejected");
        int captured = 7;
        Reject(() => card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, entity => captured), "captured state rejected");
        Func<IPlayable, int> multicast = Damage;
        multicast += Cost;
        Reject(() => card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, multicast), "multicast recipe rejected");
        card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Damage);
        Reject(() => card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Damage), "duplicate tag rejected");
        var clone = card.Clone();
        clone.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_2, Left);
        card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_2, Left);
        Verify(card.ConsumesHistory && clone.ConsumesHistory, "definition copy isolates additions and enrolls history");
    }

    private static void ZonesAndUpdates()
    {
        foreach (bool history in new[] { false, true })
        {
            foreach (int playerId in new[] { 1, 2 })
            {
                var g = HistoryFactsProof.Fixture(history);
                var p = playerId == 1 ? g.Player1 : g.Player2;
                var display = Entity.FromCard(p, DamageCard);
                p.DeckZone.Add(display);
                g.AuraUpdate();
                Verify(display[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "deck not evaluated");
                p.HandZone.Add(p.DeckZone.Remove(display));
                int before = g.PowerHistory?.Full.Count ?? 0;
                int facts = g.HistoryFacts.Count;
                g.AuraUpdate();
                Verify(display[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && display.Cost == 3, "hand display and cost aura coexist");
                Verify(g.HistoryFacts.Count == facts && g.TaskQueue.IsEmpty, "refresh adds no facts/tasks");
                if (history)
                {
                    var suffix = g.PowerHistory.Full.Skip(before).ToArray();
                    Verify(suffix.All(e => e is PowerHistoryTagChange), "refresh emits only tag changes, no POWER blocks");
                    Verify(suffix.OfType<PowerHistoryTagChange>().Any(e => e.EntityId == display.Id && e.Tag == GameTag.TAG_SCRIPT_DATA_NUM_1 && e.Value == 1),
                        "display uses ordinary tag projection path");
                }
                Kill(g, Summon(p.Opponent, "CS2_231"));
                Verify(display[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "opponent death excluded");
                Kill(g, Summon(p, "CS2_231"));
                Verify(display[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "committed friendly death refreshes");
                p.GraveyardZone.Add(p.HandZone.Remove(display));
                Kill(g, Summon(p, "CS2_231"));
                Verify(display[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "outside hand stops refreshing");
                p.HandZone.Add(p.GraveyardZone.Remove(display));
                g.AuraUpdate();
                Verify(display[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "reentry reads full history");
                var replacement = Cards.FromId("GAME_005").Clone();
                Generic.ChangeEntityBlock(p, display, replacement, false);
                int afterChange = g.IdEntityDic[display.Id][GameTag.TAG_SCRIPT_DATA_NUM_1];
                Kill(g, Summon(p, "CS2_231"));
                Verify(g.IdEntityDic[display.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == afterChange, "definition replacement stops display refresh");
            }
        }
    }

    private static void Holding()
    {
        var g = HistoryFactsProof.Fixture();
        var p = g.Player1;
        var card = Entity.FromCard(p, HeldCard, zone: p.HandZone);
        g.AuraUpdate();
        Verify(card[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && card[GameTag.TAG_SCRIPT_DATA_NUM_2] == 3, "entry midway through turn starts at zero");
        End(g);
        Verify(card[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "opponent turn excluded");
        End(g);
        Verify(card[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && card[GameTag.TAG_SCRIPT_DATA_NUM_2] == 2, "owner start updates magnitude and turns left");
        p.SetasideZone.Add(p.HandZone.Remove(card));
        Verify(Held(card) == 1, "resolution after hand removal reads same residence window");
        End(g); End(g);
        Verify(Held(card) == 1, "closed hand residence excludes subsequent owner starts");
        p.HandZone.Add(p.SetasideZone.Remove(card));
        g.AuraUpdate();
        Verify(card[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && card[GameTag.TAG_SCRIPT_DATA_NUM_2] == 3, "new residence resets holding display");
    }

    private static void Continue(Game g)
    {
        var card = g.Player1.HandZone.Single(c => c.Card.Id == DamageCard.Id);
        int value = card[GameTag.TAG_SCRIPT_DATA_NUM_1];
        Kill(g, Summon(g.Player1, "CS2_231"));
        Verify(card[GameTag.TAG_SCRIPT_DATA_NUM_1] == value + 1, "restored recipe reads branch-local history");
        End(g); End(g);
        var held = g.Player1.HandZone.Single(c => c.Card.Id == HeldCard.Id);
        Verify(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && held[GameTag.TAG_SCRIPT_DATA_NUM_2] == 2, "restored held recipe continues");
    }

    internal static void Child(string path)
    {
        Register();
        HistorySnapshotProof.Register();
        var bytes = File.ReadAllBytes(path);
        var g = Game.LoadSnapshot(bytes);
        Verify(bytes.SequenceEqual(g.SaveSnapshot()), "fresh-process byte-identical round trip");
        Continue(g);
        Console.WriteLine("PASS history hand display fresh-process snapshot continuation");
    }

    private static void Persistence()
    {
        var g = HistoryFactsProof.Fixture();
        Entity.FromCard(g.Player1, DamageCard, zone: g.Player1.HandZone);
        Entity.FromCard(g.Player1, HeldCard, zone: g.Player1.HandZone);
        g.AuraUpdate();
        var bytes = g.SaveSnapshot();
        foreach (Func<Game, Game> fork in new Func<Game, Game>[] { x => x.Clone(), x => x.CloneExact(), x => x.CloneForSimulation(42), x => Game.LoadSnapshot(bytes) })
        {
            var copy = fork(g);
            Continue(copy);
            Verify(bytes.SequenceEqual(g.SaveSnapshot()), "clone/load continuation cannot mutate source");
        }
        var custom = DamageCard.Clone();
        custom.Tags[GameTag.COST] = 7;
        var frozen = new HistoryDefinition(custom).Materialize();
        Verify(frozen.Tags[GameTag.COST] == 7 && frozen.ConsumesHistory, "detached custom definition retains display metadata");
        var materialized = HistoryFactsProof.Fixture();
        var detached = Entity.FromCard(materialized.Player1, frozen, zone: materialized.Player1.HandZone);
        materialized.AuraUpdate();
        Verify(detached[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "detached custom definition executes display recipe");
        var bad = HistoryFactsProof.Fixture();
        custom.ConsumesHistory = false;
        Entity.FromCard(bad.Player1, custom, zone: bad.Player1.HandZone);
        bool rejected = false;
        try { Game.LoadSnapshot(bad.SaveSnapshot()); } catch (SerializationException) { rejected = true; }
        Verify(rejected, "snapshot rejects invalid display enrollment metadata");
        string path = Path.Combine(Path.GetTempPath(), "hs-hand-display-" + Guid.NewGuid().ToString("N") + ".snapshot");
        try
        {
            File.WriteAllBytes(path, bytes);
            var start = new ProcessStartInfo(Environment.ProcessPath) { RedirectStandardOutput = true, RedirectStandardError = true };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--history-display-child"); start.ArgumentList.Add(path);
            using var child = Process.Start(start);
            string output = child.StandardOutput.ReadToEnd(), error = child.StandardError.ReadToEnd();
            Verify(child.WaitForExit(30000) && child.ExitCode == 0, "child: " + output + error);
            Console.Write(output);
        }
        finally { File.Delete(path); }
    }

    private static void SearchBound()
    {
        var g = HistoryFactsProof.Fixture(false); var p = g.Player1;
        var debit = Entity.FromCard(p, Cards.FromId("CS2_120"));
        while (g.HistoryFacts.Count < 10000) { p.UsedMana = 0; Generic.PayPhase(p, debit); }
        var damage = Entity.FromCard(p, DamageCard, zone: p.HandZone);
        var held = Entity.FromCard(p, HeldCard, zone: p.HandZone);
        for (int i = 0; i < 8; i++) Kill(g, Summon(p, "CS2_231"));
        End(g); End(g); End(g); End(g);
        p.BaseMana = 10; p.UsedMana = 0;
        Add(p, "CS2_182"); Add(p, "CS2_182");
        Summon(p, "CS2_182"); Summon(p, "CS2_182");
        Verify(damage[GameTag.TAG_SCRIPT_DATA_NUM_1] == 9 && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
            "long-history literal death and owner-turn values");
        string original = g.HistoryFacts.CanonicalText();
        var field = typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic);
        var timer = Stopwatch.StartNew();
        RecipeCalls = RecipeEntries = 0;
        var root = FairBotState.Create(g, p.PlayerId, 53, DeckStyle.Control);
        var projected = (Game)field.GetValue(root);
        Verify(projected.IdEntityDic[damage.Id].Card.Id == DamageCard.Id
            && projected.IdEntityDic[held.Id].Card.Id == HeldCard.Id, "search keeps owner-visible recipes active");
        int nodes = 0;
        foreach (var action in root.Actions.Where(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source != damage.Id && a.Source != held.Id).Take(8))
        {
            var child = root.Apply(action); _ = child.Evaluate(); nodes++;
            foreach (var next in child.Actions.Where(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source != damage.Id && a.Source != held.Id).Take(4))
            {
                var leaf = child.Apply(next); _ = leaf.Evaluate(); nodes++;
                var branch = (Game)field.GetValue(leaf);
                Verify(branch.IdEntityDic[damage.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 9
                    && branch.IdEntityDic[held.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
                    "search leaves retain correct active display values");
            }
        }
        Verify(nodes >= 12 && RecipeCalls >= 36, "search expands real two-ply branches and executes display recipes");
        Verify(RecipeEntries <= RecipeCalls * 8,
            $"owner-visible search bounded to eight index entries per recipe, got {RecipeEntries}/{RecipeCalls} over {g.HistoryFacts.Count} facts");
        Verify(original == g.HistoryFacts.CanonicalText(), "search leaves authoritative history unchanged");
        Console.WriteLine(FormattableString.Invariant($"PASS history display search bound: facts={g.HistoryFacts.Count}; nodes={nodes}; recipes={RecipeCalls}; entries={RecipeEntries}; wall={timer.Elapsed.TotalMilliseconds:F3}ms (informational)"));
    }

    private static void NoDisplayNeutrality()
    {
        foreach (bool trade in new[] { false, true })
        {
            var a = HistoryFactsProof.Fixture(); var b = HistoryFactsProof.Fixture();
            PlayerTask Action(Game game)
            {
                // An extra AuraUpdate after Trade would change this cost and its packet.
                new AdaptiveCostEffect(c => c.Controller.UsedMana).Activate((Playable)Add(game.Player1, "CS2_182"));
                var source = Add(game.Player1, trade ? "CORE_EX1_002" : "CS2_182");
                return trade ? TradeCardTask.Any(game.Player1, source) : PlayCardTask.Any(game.Player1, source);
            }
            Verify(a.Process(Action(a)), "no-display public action accepted");
            // The legacy core path omits the new decision-return refresh.
            Verify((bool)typeof(Game).GetMethod("ProcessCore", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(b, new object[] { Action(b), false }), "no-display legacy action accepted");
            Verify(!a.HistoryFacts.HasConsumers && !b.HistoryFacts.HasConsumers
                && a.Random.CaptureState().Equals(b.Random.CaptureState())
                && Canonical.State(a) == Canonical.State(b) && Canonical.History(a) == Canonical.History(b),
                "no-display state/history/RNG unchanged versus legacy action path");
#if CLIENT_TRANSPORT
            var packetsA = Native(a, out _); var packetsB = Native(b, out _);
            Verify(packetsA.Count == packetsB.Count && packetsA.Zip(packetsB).All(pair => pair.First.SequenceEqual(pair.Second)),
                "no-display native packets byte-identical versus legacy action path");
#endif
        }
        Console.WriteLine("PASS history display neutrality: no-display play/Trade packets and RNG identical to legacy core path");
    }

#if CLIENT_TRANSPORT
    private static List<byte[]> Native(Game g, out PegasusGame.PowerHistory history)
    {
        var packets = new List<byte[]>();
        PegasusGame.PowerHistory sent = null;
        var match = new NativeMatch((id, message) => {
            using var stream = new MemoryStream();
            message.Serialize(stream);
            packets.Add(stream.ToArray());
            sent = message as PegasusGame.PowerHistory;
        });
        typeof(NativeMatch).GetProperty("Game").SetValue(match, g);
        typeof(NativeMatch).GetMethod("SendHistory", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, new object[] { g.PowerHistory.Full });
        history = sent;
        return packets;
    }
#endif

    private static void Privacy()
    {
        foreach (int viewer in new[] { 1, 2 })
        {
            Game Fixture(bool display)
            {
                var g = HistoryFactsProof.Fixture();
                var p = viewer == 1 ? g.Player2 : g.Player1;
                Entity.FromCard(p, display ? DamageCard : Cards.FromId("GAME_005"), zone: p.HandZone);
                g.AuraUpdate();
                Kill(g, Summon(p, "CS2_231"));
                return g;
            }
            var a = Fixture(true); var b = Fixture(false);
            var fa = FairBotState.Create(a, viewer, 42, DeckStyle.Control);
            var fb = FairBotState.Create(b, viewer, 42, DeckStyle.Control);
            var field = typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic);
            var ga = (Game)field.GetValue(fa); var gb = (Game)field.GetValue(fb);
            ga.AuraUpdate(); gb.AuraUpdate();
            Verify(Canonical.State(ga) == Canonical.State(gb) && fa.Evaluate() == fb.Evaluate()
                && fa.Actions.Select(x => x.ToString()).SequenceEqual(fb.Actions.Select(x => x.ToString())),
                "both viewers: hidden display vs vanilla produces identical projected state/evaluation/actions");
            if (viewer != 1) continue; // NativeMatch's human viewer is fixed to player 1.
#if CLIENT_TRANSPORT
            var packetsA = Native(a, out var nativeA); var packetsB = Native(b, out _);
            Verify(packetsA.Count == packetsB.Count && packetsA.Zip(packetsB).All(pair => pair.First.SequenceEqual(pair.Second)),
                "opponent-visible serialized native packets byte-identical");
            int hiddenId = a.Player2.HandZone.Single().Id;
            Verify(nativeA.List.All(e => e.PowerStart?.Source != hiddenId && !(e.TagChange?.Entity == hiddenId
                    && (e.TagChange.Tag == (int)GameTag.TAG_SCRIPT_DATA_NUM_1 || e.TagChange.Tag == (int)GameTag.TAG_SCRIPT_DATA_NUM_2))),
                "no hidden-source POWER block or script value escapes");
            var own = HistoryFactsProof.Fixture();
            var visible = Entity.FromCard(own.Player1, DamageCard, zone: own.Player1.HandZone);
            own.AuraUpdate(); Kill(own, Summon(own.Player1, "CS2_231"));
            Native(own, out var ownerPacket);
            Verify(ownerPacket.List.Any(e => e.TagChange?.Entity == visible.Id && e.TagChange.Tag == (int)GameTag.TAG_SCRIPT_DATA_NUM_1 && e.TagChange.Value == 2),
                "owner receives refreshed text value through native projection");
#endif
        }
    }
}
