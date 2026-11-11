using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Snapshots;

internal static class SnapshotSchemaExtensionProof
{
    // Exact reviewed pre-achievements engine, commit 7efb64a (same engine as 84eb0cf), netstandard2.1 Release.
    private const int PublishedTypeCount = 468;
    private const string PublishedIdsHash = "a2f7816f12d372b71fefa8c7548bc7c397ad64afac855106eee702a50289f8f9";
    private const string PublishedFingerprint = "fNh+dw2OJ5+KWAYh/kI4LhrCuS/1BcGcF2/GPU8jtJ4=";

    [GroupProof("snapshot-schema-ids")]
    internal static void Run()
    {
        var types = (Type[])typeof(SnapshotSchema).GetField("Types", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        string published = string.Join("\n", types.Take(PublishedTypeCount).Select((type, id) => id + ":" + type.FullName));
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(published))).ToLowerInvariant();
        Core2026Proof.Check(hash == PublishedIdsHash, "all published snapshot type IDs retain the reviewed pre-achievements mapping");
        Core2026Proof.Check(SnapshotSchema.Id(typeof(HistoryRecorder.PlayerActionScope)) >= PublishedTypeCount
            && SnapshotSchema.Id(typeof(HistoryRecorder).GetNestedType("TransferScope", BindingFlags.NonPublic)) >= PublishedTypeCount,
            "both achievement recorder scopes append after the published schema");
        Core2026Proof.Check(SnapshotSchema.Id(typeof(SabberStoneCore.Kettle.PowerHistoryShuffleDeck)) < PublishedTypeCount,
            "published ShuffleDeck keeps its original ID before achievement extensions");
        Core2026Proof.Check(SnapshotSchema.Fingerprint != PublishedFingerprint, "new recorder state changes the snapshot layout fingerprint");
        byte[] bytes = OfflinePoolSnapshotProof.Fixture().SaveSnapshot();
        using var input = new MemoryStream(bytes);
        using var reader = new BinaryReader(input, Encoding.UTF8, true);
        string magic = reader.ReadString(); int version = reader.ReadInt32(); reader.ReadString();
        long tail = input.Position;
        using var incompatible = new MemoryStream();
        using (var writer = new BinaryWriter(incompatible, Encoding.UTF8, true)) {
            writer.Write(magic); writer.Write(version); writer.Write(PublishedFingerprint);
            writer.Write(bytes, (int)tail, bytes.Length - (int)tail);
        }
        bool rejected = false;
        try { Game.LoadSnapshot(incompatible.ToArray()); } catch (SerializationException) { rejected = true; }
        Core2026Proof.Check(rejected, "old snapshot fingerprint rejects incompatible layout without migration");
        Console.WriteLine("PASS snapshot schema IDs: complete published mapping stable; recorder scopes appended; incompatible fingerprint rejected");
    }
}
