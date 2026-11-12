using System.Xml.Linq;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Model;

internal static class PastPoolVarietyProof
{
    [GroupProof("past-pool-variety")]
    internal static void Run()
    {
        // A scratch catalog allows the same proof to demonstrate the pre-expansion failure.
        var path = Environment.GetEnvironmentVariable("HS_PAST_POOL_VARIETY_CATALOG");
        var manifest = (string.IsNullOrEmpty(path) ? OfflinePoolPolicy.Load()
            : OfflinePoolPolicy.Parse(XDocument.Load(path))).BuildManifest();
        var failures = new List<string>();
        int requirements = 0;
        foreach (var source in OfflinePoolDeclarations.Cards)
        foreach (var pool in source.Pools.Where(p => ContainsPast(p.Filter)))
        {
            requirements++;
            var classes = UsesController(pool.Filter) ? pool.Classes : pool.Classes.Take(1);
            foreach (var hero in classes)
            {
                int count = OfflineCardPools.Candidates(manifest, PoolRequirement.HeroClass(hero),
                    pool.Filter, source.DbfId, pool.Destination).Select(c => c.AssetId).Distinct().Count();
                var label = $"{source.CardId}/{pool.Name}/{hero}";
                Console.WriteLine($"PAST POOL {label}={count}");
                if (count < 10) failures.Add($"{label}={count}" +
                    (pool.Filter.CardIds == null ? "" : $" (curated maximum {pool.Filter.CardIds.Count})"));
            }
        }
        Core2026Proof.Check(requirements > 0, "Past declarations discovered from registry");
        Core2026Proof.Check(failures.Count == 0, "Past pools require >=10 admitted candidates: " + string.Join("; ", failures));
        Console.WriteLine($"PASS Past variety: {requirements} requirements, catalog {manifest.Hash}");
    }

    private static bool ContainsPast(OfflinePoolFilter filter) =>
        filter.Scope == PoolScope.Past || filter.AnyOf.Any(ContainsPast);

    private static bool UsesController(OfflinePoolFilter filter) =>
        filter.ClassMode == PoolClassMode.ControllerAndNeutral || filter.AnyOf.Any(UsesController);
}
