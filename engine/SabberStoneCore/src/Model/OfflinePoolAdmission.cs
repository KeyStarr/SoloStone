using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Model
{
    /// <summary>Runs only after Cards and the complete module registry have loaded; never in their initializers.</summary>
    public static class OfflinePoolAdmission
    {
        // A Discover offers three cards, so each random pool needs at least this many distinct candidates for every
        // class that can play the card. A registered card with a smaller pool fails validation ("pool too small"),
        // except for the pools that opt out (thin exact-cost pools; curated pools with unimplemented cards use a lower
        // minimum).
        public const int MinimumCandidates = 3;
        public static Report Evaluate(OfflinePoolManifest manifest, IEnumerable<OfflinePoolDeclaration> declarations = null,
            IReadOnlyDictionary<int, string> registeredModules = null)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            var registry = registeredModules ?? EffectModules.ByDbfId;
            var sources = (declarations ?? OfflinePoolDeclarations.Cards).ToArray();
            if (sources.Select(d => d.DbfId).Distinct().Count() != sources.Length || sources.Select(d => d.CardId).Distinct().Count() != sources.Length)
                throw new InvalidDataException("Duplicate pool declaration identity.");
            return new Report(manifest, sources.Select(source => {
                bool registered = registry.TryGetValue(source.DbfId, out var id);
                if ((registered && id != source.CardId) || registry.Any(p => p.Value == source.CardId && p.Key != source.DbfId))
                    throw new InvalidDataException("Pool declaration registry identity mismatch: " + source.CardId);
                var counts = new List<RequirementCount>();
                foreach (var pool in source.Pools)
                {
                    var missing = pool.AllowUnimplementedCuratedCards ? pool.Filter.CardIds.Where(cardId => {
                        var card = Cards.All.SingleOrDefault(c => c.Id == cardId);
                        if (card == null || card.AssetId <= 0 || !card.Collectible ||
                            (pool.Filter.Type.HasValue && card.Type != pool.Filter.Type))
                            throw new InvalidDataException("Curated exemption requires retail collectible identities: " + cardId);
                        return !card.Implemented && !registry.Values.Contains(cardId) &&
                            !manifest.Standard.Concat(manifest.Past).Any(m => m.CardId == cardId);
                    }).ToArray() : Array.Empty<string>();
                    int minimum = pool.AllowThinExactCost ? 0 : pool.AllowUnimplementedCuratedCards
                        ? Math.Min(MinimumCandidates, pool.Filter.CardIds.Count - missing.Length) : MinimumCandidates;
                    foreach (string hero in pool.Classes)
                    {
                        void Count(OfflinePoolFilter filter, string binding) => counts.Add(new RequirementCount(pool.Name, hero,
                            filter, binding, OfflineCardPools.Candidates(manifest, PoolRequirement.HeroClass(hero), filter, source.DbfId, pool.Destination)
                                .Select(c => c.AssetId).Distinct().Count(), pool.AllowThinExactCost, minimum, missing));
                        Count(pool.Filter, pool.CostBinding == PoolCostBinding.None ? "fixed" : "static-envelope");
                        if (pool.CostBinding != PoolCostBinding.None) Count(pool.CostBinding == PoolCostBinding.StackNumberMaxCost
                            ? pool.Filter.WithMaxCost(pool.OrdinaryCost.Value) : pool.Filter.WithCost(pool.OrdinaryCost.Value), "ordinary");
                    }
                }
                return new CardResult(source, registered, counts.ToArray());
            }).ToArray());
        }

        public sealed class RequirementCount
        {
            public string Name { get; }
            public string Class { get; }
            public OfflinePoolFilter Filter { get; }
            public string Binding { get; }
            public int Count { get; }
            public bool AllowThinExactCost { get; }
            public int MinimumRequiredCandidates { get; }
            public IReadOnlyList<string> UnimplementedCuratedCardIds { get; }
            internal RequirementCount(string name, string hero, OfflinePoolFilter filter, string binding, int count, bool allowThinExactCost,
                int minimum, string[] missing)
            {
                Name = name; Class = hero; Filter = filter; Binding = binding; Count = count; AllowThinExactCost = allowThinExactCost;
                MinimumRequiredCandidates = minimum; UnimplementedCuratedCardIds = Array.AsReadOnly(missing);
            }
        }
        public sealed class Identity
        {
            public string CardId { get; }
            public int DbfId { get; }
            internal Identity(OfflinePoolPolicy.Member member) { CardId = member.CardId; DbfId = member.DbfId; }
        }
        public sealed class CardResult
        {
            public string CardId { get; }
            public int DbfId { get; }
            public bool Registered { get; }
            public IReadOnlyList<RequirementCount> PoolRequirements { get; }
            internal CardResult(OfflinePoolDeclaration source, bool registered, RequirementCount[] counts)
            { CardId = source.CardId; DbfId = source.DbfId; Registered = registered; PoolRequirements = Array.AsReadOnly(counts); }
        }
        public sealed class Report
        {
            [NonSerialized] private readonly IReadOnlyList<string> activeSets;
            public int SchemaVersion => 1;
            public string PoolManifestHash { get; }
            public IReadOnlyList<Identity> EligibleMembers { get; }
            public IReadOnlyList<CardResult> Cards { get; }
            internal Report(OfflinePoolManifest manifest, CardResult[] cards)
            {
                activeSets = manifest.Sets;
                PoolManifestHash = manifest.Hash;
                EligibleMembers = Array.AsReadOnly(manifest.Standard.Concat(manifest.Past).OrderBy(m => m.DbfId).Select(m => new Identity(m)).ToArray());
                Cards = Array.AsReadOnly(cards);
            }
            public void ValidateRegistered()
            {
                var definitions = SabberStoneCore.Model.Cards.All.ToDictionary(c => c.Id, StringComparer.Ordinal);
                var policy = OfflinePoolPolicy.Load();
                var failures = Cards.Where(c => c.Registered && (!definitions.TryGetValue(c.CardId, out var card)
                    || policy.AllowsCard(card, activeSets))).SelectMany(c => c.PoolRequirements
                    .Where(r => r.Count < r.MinimumRequiredCandidates && !(r.Filter.MinCost.HasValue && r.Filter.MinCost == r.Filter.MaxCost)).Select(r =>
                        $"{c.CardId}/{c.DbfId} class={r.Class} filter={r.Name} binding={r.Binding} count={r.Count}"));
                if (failures.Any()) throw new InvalidDataException("pool too small: " + string.Join("; ", failures));
            }
        }
    }
}
