using System.Text.Json;
using System.Security.Cryptography;
using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static class OfflinePoolReplayProof
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static void Check(bool value, string label) => OfflinePoolFixtures.Check(value, "replay: " + label);
    private static GameRecord Copy(GameRecord record) => JsonSerializer.Deserialize<GameRecord>(JsonSerializer.Serialize(record, Json), Json);

    private static void Reject(GameRecord record, string invariant)
    {
        int started = 0, actions = 0;
        try { Simulation.Run(record, replay: true, started: _ => started++, processed: _ => actions++); }
        catch (InvariantException error) when (error.Invariant == invariant)
        {
            Check(started == 0 && actions == 0, "mismatch rejected before start callback/recorded actions");
            return;
        }
        throw new Exception("offline pool replay accepted " + invariant);
    }

    internal static void Run()
    {
        var record = new GameRecord { Id = "offline-pool-replay", Suite = "classic", Player = "mage", Opponent = "warrior", Seed = 7 };
        var played = Simulation.Run(record);
        Simulation.Finish(record, played);
        Check(PoolSnapshots.Save().Hash == record.PoolManifestHash, "recording persists its installed snapshot");
        Check(record.ReplaySchema == 3 && record.PoolPolicyVersion == played.OfflinePools.PolicyVersion &&
            record.PoolManifestHash == played.OfflinePools.Hash && record.Actions.Count > 0, "new recording pins schema/policy/hash");
        string originalDirectory = Environment.CurrentDirectory;
        string missingDirectory = Path.Combine(Path.GetTempPath(), "missing-pool-" + Guid.NewGuid());
        Directory.CreateDirectory(missingDirectory);
        try
        {
            Environment.CurrentDirectory = missingDirectory;
            // The installed catalog still matches: a missing persisted snapshot must not fall back.
            Reject(Copy(record), "replay.pool");
        }
        finally { Environment.CurrentDirectory = originalDirectory; Directory.Delete(missingDirectory); }
        var restored = Copy(record);
        var replayed = Simulation.Run(restored, replay: true);
        Check(Canonical.State(played) == Canonical.State(replayed) && Canonical.History(played) == Canonical.History(replayed), "schema-3 JSON round trip");
        Check(played.Random.CaptureState().Equals(replayed.Random.CaptureState()) &&
            OfflineCardPools.SampleDistinct(played, OfflinePoolFilter.Random(), CardClass.MAGE, 0).Select(c => c.Id)
            .SequenceEqual(OfflineCardPools.SampleDistinct(replayed, OfflinePoolFilter.Random(), CardClass.MAGE, 0).Select(c => c.Id)), "replay preserves future pool offers/RNG");

        // A replay's pool may differ from the installed catalog; its own hash must still match.
        var xml = OfflinePoolFixtures.Catalog();
        xml.Root.Element("Members").Elements("Card").Last().Remove();
        var pinned = OfflinePoolFixtures.Manifest(xml);
        string path = $"tests/goldens/pools/{pinned.Hash}.xml";
        string externalDirectory = Path.Combine(Path.GetTempPath(), "pool-replay-" + Guid.NewGuid());
        Directory.CreateDirectory(externalDirectory);
        string external = Path.Combine(externalDirectory, pinned.Hash + ".xml");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        try
        {
            xml.Save(external);
            File.WriteAllText(external + ".sha256", Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(external))) + "\n");
            Check(PoolSnapshots.Save(external).Hash == pinned.Hash && File.Exists(path), "recording stores supplied historical snapshot");
            var historical = Copy(record); historical.PoolManifestHash = pinned.Hash;
            var historicalGame = Simulation.Run(historical, replay: true);
            Check(historicalGame.OfflinePools.Hash == pinned.Hash, "historical membership selected before start");
            Check(Canonical.State(played) == Canonical.State(historicalGame), "current definitions and strict actions survive catalog drift");
            var boundary = Simulation.Create(historical, replay: true);
            var bytes = boundary.SaveSnapshot();
            var loaded = Game.LoadSnapshotWithPools(bytes, pinned);
            Check(loaded.OfflinePools.Hash == pinned.Hash && Canonical.State(boundary) == Canonical.State(loaded)
                && boundary.Random.CaptureState().Equals(loaded.Random.CaptureState()), "explicit historical snapshot load keeps state/pool/RNG");
            bool wrongPool = false;
            try { Game.LoadSnapshotWithPools(bytes, played.OfflinePools); }
            catch (System.Runtime.Serialization.SerializationException) { wrongPool = true; }
            Check(wrongPool, "snapshot load rejects a different supplied pool");
            byte[] original = File.ReadAllBytes(path);
            foreach (string field in new[] { "attack", "health", "proofKey" })
            {
                var corrupt = new System.Xml.Linq.XDocument(xml);
                var row = corrupt.Root.Element("Members").Elements("Card").Single(c => (string)c.Attribute("cardId") == "EX1_028");
                row.SetAttributeValue(field, field == "proofKey" ? "tampered-proof" : (int)row.Attribute(field) + 1);
                Check(OfflinePoolFixtures.Manifest(corrupt).Hash == pinned.Hash, field + " is outside legacy manifest digest");
                corrupt.Save(path);
                Reject(Copy(historical), "replay.pool");
                File.WriteAllBytes(path, original);
            }
            string checksum = File.ReadAllText(path + ".sha256");
            File.Delete(path + ".sha256"); Reject(Copy(historical), "replay.pool");
            File.WriteAllText(path + ".sha256", new string('0', 64)); Reject(Copy(historical), "replay.pool");
            File.WriteAllText(path + ".sha256", checksum);
            xml.Root.Element("Members").Elements("Card").Last().Remove(); xml.Save(path);
            File.WriteAllText(path + ".sha256", Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) + "\n");
            Reject(historical, "replay.pool");
            File.WriteAllText(path, "<broken>");
            File.WriteAllText(path + ".sha256", Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) + "\n");
            Reject(historical, "replay.pool");
        }
        finally
        {
            File.Delete(path); File.Delete(path + ".sha256");
            File.Delete(external); File.Delete(external + ".sha256"); Directory.Delete(externalDirectory);
        }

        void Bad(Action<GameRecord> mutate, string invariant = "replay.pool")
        {
            var bad = Copy(record); mutate(bad);
            // If compatibility checks run late, even the first recorded action is invalid.
            bad.Actions[0] = int.MaxValue;
            Reject(bad, invariant);
        }
        Bad(r => r.PoolPolicyVersion = "offline-standard/0");
        Bad(r => r.PoolPolicyVersion = null);
        Bad(r => r.PoolManifestHash = null);
        Bad(r => r.PoolManifestHash = "malformed");
        Bad(r => r.PoolManifestHash = new string('0', 64));
        Bad(r => r.PoolManifestHash = OfflinePoolFixtures.Manifest(OfflinePoolFixtures.Fixture()).Hash);
        Bad(r => r.ReplaySchema = null, "replay.schema");
        Bad(r => r.ReplaySchema = 4, "replay.schema");
        Bad(r => r.ReplaySchema = 2); // Metadata cannot be downgraded into a legacy record.

        var legacy = Copy(record);
        legacy.ReplaySchema = 2; legacy.PoolPolicyVersion = null; legacy.PoolManifestHash = null;
        var legacyGame = Simulation.Create(legacy, replay: true);
        foreach (var game in new[] { legacyGame, legacyGame.CloneExact(), legacyGame.CloneForSimulation(91), Game.LoadSnapshot(legacyGame.SaveSnapshot()) })
        {
            var rng = game.Random.CaptureState();
            foreach (Action call in new Action[] {
                () => OfflineCardPools.Candidates(game, CardClass.MAGE, OfflinePoolFilter.Random(), 0),
                () => OfflineCardPools.Pick(game, OfflinePoolFilter.Random(), CardClass.MAGE, 0),
                () => OfflineCardPools.SampleDistinct(game, OfflinePoolFilter.Random(), CardClass.MAGE, 0) })
            {
                bool rejected = false;
                try { call(); } catch (InvalidOperationException error) { rejected = error.Message.Contains("legacy replay"); }
                Check(rejected && rng.Equals(game.Random.CaptureState()), "schema-2 primitive forbidden, including clones/load; no RNG");
            }
        }
        var legacyPlayed = Simulation.Run(legacy, replay: true);
        Check(Canonical.Sha(Canonical.State(legacyPlayed)) == record.StateSha &&
            Canonical.Sha(Canonical.History(legacyPlayed)) == record.HistorySha, "legacy mode keeps canonical bytes");
        Console.WriteLine("PASS offline pool replay: schema-3 JSON round trip/future draws; missing snapshot, complete XML checksum, manifest and malformed XML rejections before actions; schema-2 primitive blocked across clone/load");
    }
}
