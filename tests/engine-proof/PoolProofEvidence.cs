using System.Security.Cryptography;
using System.Text.Json;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Model;
using SabberStoneCore.src.Loader;

// Receipts are emitted only after the real per-card scenario returns successfully.
internal static class PoolProofEvidence
{
    private static readonly Dictionary<string, string> Proofs = new(StringComparer.Ordinal);
    internal static void Passed(string id, string key) => Proofs[id] = key;
    internal static bool HasPassed(string id) => Proofs.ContainsKey(id);
    internal static bool HasPassed(string id, Action proof) =>
        Proofs.GetValueOrDefault(id) == proof.Method.DeclaringType.Name + "." + proof.Method.Name;

    internal static void Write(string path)
    {
        var standard = StandardSets.CollectibleIds.ToHashSet(StringComparer.Ordinal);
        var cards = Cards.All.Where(c => EffectModules.ByDbfId.ContainsKey(c.AssetId)
            || Reprints.ByDbfId.ContainsKey(c.AssetId) || RebalancedCore2026.ByDbfId.ContainsKey(c.AssetId)
            || CardDefs.Instance.Get.ContainsKey(c.Id)).ToDictionary(c => c.Id, c => new {
                dbfId = c.AssetId, implemented = c.Implemented, collectible = c.Collectible,
                registrationKind = standard.Contains(c.Id) ? "standard-module" :
                    EffectModules.ByDbfId.ContainsKey(c.AssetId) ? "effect-module" :
                    Reprints.ByDbfId.ContainsKey(c.AssetId) ? "core-reprint" :
                    RebalancedCore2026.ByDbfId.ContainsKey(c.AssetId) ? "core-rebalanced" : "legacy",
                originalId = Reprints.ByDbfId.TryGetValue(c.AssetId, out int original) ? Cards.FromAssetId(original).Id :
                    RebalancedCore2026.ByDbfId.GetValueOrDefault(c.AssetId),
                entourage = c.Entourage ?? Array.Empty<string>(),
                heroPower = c[SabberStoneCore.Enums.GameTag.HERO_POWER] > 0 ? Cards.FromAssetId(c[SabberStoneCore.Enums.GameTag.HERO_POWER])?.Id : null
            });
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllText(path, JsonSerializer.Serialize(new {
            schema = 1, engineSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(Game).Assembly.Location))),
            proofs = Proofs, registrations = cards
        }) + "\n");
    }
}
