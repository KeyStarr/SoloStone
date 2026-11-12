using HsSim;
using System.Xml.Linq;
using SabberStoneCore.Model;
using EngineGame = SabberStoneCore.Model.Game;

// Seeded identity fixtures share the accepted golden pool; card definitions remain live.
// Hash is the pool snapshot the golden replays were recorded with. Proofs that assert exact seeded picks use it, so
// admitting new cards to the live pool does not break them; behaviour changes still do, because definitions come from
// the current engine.
internal static class PinnedPoolFixture
{
    internal const string Hash = "29ea592977fe61745aacb70dfe5f511ab240e9b051df570bdb2495b7b704b4d0";
    private static readonly Lazy<OfflinePoolManifest> Pinned = new(() => PoolSnapshots.Read(PoolSnapshots.PathFor(Hash), Hash));
    internal static OfflinePoolManifest Manifest => Pinned.Value;
    internal static XDocument Catalog() => XDocument.Load(PoolSnapshots.PathFor(Hash));
    internal static EngineGame Use(EngineGame game)
    {
        typeof(EngineGame).GetProperty(nameof(EngineGame.OfflinePools)).SetValue(game, Manifest);
        return game;
    }
    internal static EngineGame LoadSnapshot(byte[] bytes) => EngineGame.LoadSnapshotWithPools(bytes, Manifest);
}
