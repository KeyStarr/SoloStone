using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Snapshots;

internal static class OfflinePoolSnapshotProof
{
    private static void Check(bool value, string label) => OfflinePoolFixtures.Check(value, "snapshot: " + label);
    internal static Game Fixture(bool start = true)
    {
        var game = new Game(new GameConfig { RandomSeed = 71, StartPlayer = 1, Shuffle = false,
            Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.WARRIOR,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            History = true, Logging = false, SkipMulligan = false });
        if (start) game.StartGame();
        return game;
    }

    private static string Members(Game game) => string.Join("|", game.OfflinePools.Standard.Concat(game.OfflinePools.Past)
        .Select(m => m.DbfId + ":" + m.CardId));

    private static string Future(Game game)
    {
        var draws = new List<string>();
        foreach (var scope in new[] { PoolScope.Standard, PoolScope.Past })
        for (int i = 0; i < 12; i++)
        {
            var filter = OfflinePoolFilter.Random(scope: scope);
            draws.Add(OfflineCardPools.Pick(game, filter, CardClass.MAGE, 0)?.Id);
            draws.Add(string.Join(",", OfflineCardPools.SampleDistinct(game, filter, CardClass.MAGE, 0).Select(c => c.Id)));
        }
        return Canonical.Sha(Members(game) + game.OfflinePools.Hash + string.Join("|", draws) + Canonical.Value(game.Random.CaptureState()));
    }

    private static void RoundTrip(Game game)
    {
        byte[] bytes = game.SaveSnapshot();
        Check(bytes.Length < 128 * 1024, "snapshot stores pool identity, not 310 KB of catalog metadata");
        var loaded = Game.LoadSnapshot(bytes);
        Check(ReferenceEquals(game.OfflinePools, loaded.OfflinePools) && ReferenceEquals(game.OfflinePools, Fixture().OfflinePools),
            "new games and loaded snapshots share the installed manifest");
        var clone = game.CloneExact(history: true);
        Check(ReferenceEquals(game.OfflinePools, clone.OfflinePools), "exact clone shares manifest");
        Check(Members(game) == Members(loaded) && game.OfflinePools.Hash == loaded.OfflinePools.Hash, "loaded members/hash");
        Check(bytes.SequenceEqual(loaded.SaveSnapshot()), "snapshot v2 byte round trip");
        Check(Canonical.State(game) == Canonical.State(loaded) && Canonical.History(game) == Canonical.History(loaded), "load leaves state/history unchanged");
        Check(game.GetPendingDecisions().Select(d => d.ToJson()).SequenceEqual(loaded.GetPendingDecisions().Select(d => d.ToJson())), "open offers unchanged");
        string expected = Future(game);
        Check(Future(clone) == expected && Future(loaded) == expected, "exact clone/load future picks/offers/RNG");
    }

    internal static void Set(object target, string property, object value) => target.GetType()
        .GetField("<" + property + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    // Deliberately bypass Save's validation to exercise the actual untrusted-load boundary.
    private static byte[] Raw(Game game, string hash, string policyVersion = "offline-standard/1")
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write("SabberStone/decision-snapshot"); writer.Write(2);
        writer.Write(SnapshotSchema.Fingerprint); writer.Write(typeof(Game).Module.ModuleVersionId.ToString("N"));
        writer.Write(policyVersion); writer.Write(hash);
        var codec = new SnapshotCodec();
        typeof(SnapshotCodec).GetField("_writer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(codec, writer);
        typeof(SnapshotCodec).GetMethod("Write", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(codec, new object[] { game, 0 });
        writer.Flush(); return stream.ToArray();
    }

    private static void Reject(byte[] bytes, string label)
    {
        try { Game.LoadSnapshot(bytes); }
        catch (SerializationException) { return; }
        throw new Exception("offline pool snapshot accepted " + label);
    }

    private static void ImmutableInstalled(byte[] bytes)
    {
        var loaded = Game.LoadSnapshot(bytes);
        void RejectMutation(Action mutate)
        {
            bool rejected = false;
            try { mutate(); } catch (NotSupportedException) { rejected = true; }
            Check(rejected, "installed collections reject writes");
        }
        RejectMutation(() => ((IList<string>)loaded.OfflinePools.Sets)[0] = "MUTATED");
        foreach (var members in new[] { loaded.OfflinePools.Standard, loaded.OfflinePools.Past })
        {
            RejectMutation(() => ((IList<OfflinePoolPolicy.Member>)members)[0] = members[0]);
            foreach (var member in members)
            {
                foreach (var values in new[] { member.Classes, member.Races, member.DependencyIds })
                    RejectMutation(() => ((IList<string>)values).Add("MUTATED"));
                RejectMutation(() => ((IDictionary<int, int>)member.Tags)[int.MaxValue] = 1);
            }
        }
        Check(bytes.SequenceEqual(loaded.SaveSnapshot()), "rejected mutations preserve snapshot bytes");
    }

    private static void FilledDeckStart()
    {
        foreach (bool predictable in new[] { false, true })
        foreach (bool randomDraw in new[] { false, true })
        {
            Game Make(bool fill) => new(new GameConfig { RandomSeed = 71, StartPlayer = -1,
                Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.WARRIOR,
                FillDecks = fill, FillDecksPredictably = predictable, DrawWithRandom = randomDraw,
                Shuffle = true, History = true, Logging = false });
            var game = Make(true);
            Check(game.Random.CaptureState().Equals(new Util.DeepCloneableRandom(71).CaptureState()) &&
                game.Player1.DeckZone.Count == 0 && game.Player2.DeckZone.Count == 0,
                "FillDecks constructor must not draw RNG or fill before manifest pinning");
            // Reproduce the old order explicitly: fill player 1, fill player 2, then start.
            var reference = Make(false);
            int constructorHistoryCount = reference.PowerHistory.Full.Count;
            reference.Player1.DeckZone.Fill(predictable ? GameConfig.UnPredictableCardIDs : null);
            reference.Player2.DeckZone.Fill(predictable ? GameConfig.UnPredictableCardIDs : null);
            reference.StartGame();
            var clone = game.CloneExact(history: true);
            game.StartGame(); clone.StartGame();
            var loaded = Game.LoadSnapshot(game.SaveSnapshot());
            foreach (var actual in new[] { game, clone, loaded })
                Check(actual.Random.CaptureState().Equals(reference.Random.CaptureState()) &&
                    Canonical.State(actual) == Canonical.State(reference), "deferred fill preserves legacy state/RNG through clone/load");
            Check(Canonical.History(game) == Canonical.History(reference) && Canonical.History(loaded) == Canonical.History(reference),
                "deferred fill preserves legacy history");
            // Clones collect future history only, without the source's constructor packets.
            Check(Canonical.Value(clone.PowerHistory.Full) == Canonical.Value(reference.PowerHistory.Full.Skip(constructorHistoryCount).ToArray()),
                "unstarted clone emits the same fill/start history");
        }
    }

    internal static void Child(string directory)
    {
        byte[] bytes = File.ReadAllBytes(Path.Combine(directory, "pool.snapshot"));
        var game = Game.LoadSnapshot(bytes);
        Check(bytes.SequenceEqual(game.SaveSnapshot()), "fresh process snapshot bytes");
        Check(Future(game) == File.ReadAllText(Path.Combine(directory, "future")), "fresh process members/offers/future draws");
        Console.WriteLine("PASS offline pool snapshot fresh-process restore");
    }

    private static void FreshProcess(Game game)
    {
        string directory = Path.Combine(Path.GetTempPath(), "hs-offline-pools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(Path.Combine(directory, "pool.snapshot"), game.SaveSnapshot());
            File.WriteAllText(Path.Combine(directory, "future"), Future(game.CloneExact()));
            var start = new ProcessStartInfo(Environment.ProcessPath) { RedirectStandardOutput = true, RedirectStandardError = true };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--offline-pool-snapshot-child"); start.ArgumentList.Add(directory);
            using var child = Process.Start(start);
            var output = child.StandardOutput.ReadToEndAsync(); var error = child.StandardError.ReadToEndAsync();
            if (!child.WaitForExit(30000)) { child.Kill(); throw new Exception("offline pool snapshot child timed out"); }
            Check(child.ExitCode == 0, "fresh process: " + output.Result + error.Result);
            Console.Write(output.Result);
        }
        finally { Directory.Delete(directory, true); }
    }

    internal static void Run()
    {
        var unstarted = Fixture(false);
        Check(unstarted.OfflinePools == null, "manifest initialized at StartGame, not lazily or in constructor");
        var beforeRng = unstarted.Random.CaptureState();
        int beforeEntities = unstarted.IdEntityDic.Count;
        unstarted.StartGame(stopBeforeShuffling: true);
        var manifest = unstarted.OfflinePools;
        Check(manifest != null && beforeRng.Equals(unstarted.Random.CaptureState()) && beforeEntities == unstarted.IdEntityDic.Count,
            "manifest build consumes no RNG and creates no entities");
        unstarted.StartGame(stopBeforeShuffling: true);
        Check(ReferenceEquals(manifest, unstarted.OfflinePools), "start never rebuilds pinned manifest");

        var game = Fixture();
        RoundTrip(game); // Before generation, at open mulligan.
        RoundTrip(game); // After generation advanced the stream.
        Simulation.Process(game, ChooseTask.Mulligan(game.Player2, new List<int>()));
        Simulation.Process(game, ChooseTask.Mulligan(game.Player1, new List<int>()));
        var offers = OfflineCardPools.SampleDistinct(game, OfflinePoolFilter.Discover(), CardClass.MAGE, game.Player1.Hero.Card.AssetId);
        Check(Generic.CreateChoiceCards(game.Player1, game.Player1.Hero, null, ChoiceType.GENERAL, ChoiceAction.HAND, offers, null), "open pre-sampled ordinary choice");
        Check(game.Player1.Choice.Choices.Select(id => game.IdEntityDic[id].Card.Id).SequenceEqual(offers.Select(c => c.Id)), "materialized offers match pool sample");
        RoundTrip(game); // Existing choice persistence, without adding effect/Discover integration.
        var liveRng = game.Random.CaptureState();
        byte[] liveBytes = game.SaveSnapshot();
        var simulation = game.CloneForSimulation(934);
        Check(ReferenceEquals(game.OfflinePools, simulation.OfflinePools) && !ReferenceEquals(game.Random, simulation.Random), "simulation shares manifest with independent RNG");
        Check(simulation.Random.CaptureState().Equals(new Util.DeepCloneableRandom(934).CaptureState()), "simulation uses derived seed");
        Check(Future(simulation) == Future(game.CloneForSimulation(934)), "same simulation seed, same future");
        Check(liveRng.Equals(game.Random.CaptureState()) && liveBytes.SequenceEqual(game.SaveSnapshot()), "search never advances authoritative state/RNG");
        FreshProcess(game);
        ImmutableInstalled(liveBytes);
        // Warm catalog registration is immutable in production: start/load must reuse it, not revalidate per game.
        var admitted = Cards.FromId(game.OfflinePools.Standard[0].CardId);
        bool implemented = admitted.Implemented;
        try
        {
            admitted.Implemented = false;
            Check(ReferenceEquals(game.OfflinePools, Fixture().OfflinePools) &&
                ReferenceEquals(game.OfflinePools, Game.LoadSnapshot(liveBytes).OfflinePools), "cached manifest avoids repeated registration validation");
        }
        finally { admitted.Implemented = implemented; }
        FilledDeckStart();

        using (var reader = new BinaryReader(new MemoryStream(liveBytes)))
        {
            reader.ReadString(); Check(reader.ReadInt32() == 2, "snapshot envelope v2");
            reader.ReadString(); reader.ReadString();
            Check(reader.ReadString() == game.OfflinePools.PolicyVersion && reader.ReadString() == game.OfflinePools.Hash, "envelope policy and manifest hash");
        }
        Reject(Raw(game, ""), "missing envelope hash");
        Reject(Raw(game, new string('0', 64)), "mismatched envelope hash");
        Reject(Raw(game, game.OfflinePools.Hash, ""), "missing policy version");
        Reject(Raw(game, game.OfflinePools.Hash, "offline-standard/999"), "different policy version");
        Reject(Raw(game, OfflinePoolFixtures.Manifest(OfflinePoolFixtures.Fixture()).Hash), "different catalog hash");
        Console.WriteLine("PASS offline pool snapshot: start ordering; immutable clone identity; independent search RNG; members/offers/future draws; v2 compact identity; cached installed manifest; 5 missing/mismatched policy/hash rejections");
    }
}
