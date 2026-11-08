using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Model
{
    /// <summary>Printed class domain and the same immutable filter template used by the recipe.</summary>
    public sealed class PoolRequirement
    {
        public static IReadOnlyList<string> PlayableClasses { get; } = Array.AsReadOnly(new[] {
            "DEATHKNIGHT", "DEMONHUNTER", "DRUID", "HUNTER", "MAGE", "PALADIN", "PRIEST", "ROGUE", "SHAMAN", "WARLOCK", "WARRIOR" });
        public string Name { get; }
        public OfflinePoolFilter Filter { get; }
        public PoolDestination Destination { get; }
        public IReadOnlyList<string> ClassDomain { get; }
        public PoolCostBinding CostBinding { get; }
        public int? OrdinaryCost { get; }
        public bool AllowThinExactCost { get; }
        public bool AllowUnimplementedCuratedCards { get; }

        public PoolRequirement(string name, OfflinePoolFilter filter, IEnumerable<string> classDomain,
            PoolCostBinding costBinding = PoolCostBinding.None, int? ordinaryCost = null, bool allowThinExactCost = false,
            bool allowUnimplementedCuratedCards = false, PoolDestination destination = PoolDestination.Hand)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Pool requirement needs a stable name.", nameof(name));
            if (!Enum.IsDefined(typeof(PoolDestination), destination)) throw new ArgumentOutOfRangeException(nameof(destination));
            var classes = (classDomain ?? throw new ArgumentNullException(nameof(classDomain))).ToArray();
            if (classes.Length == 0 || classes.Distinct().Count() != classes.Length ||
                classes.Any(c => c != "NEUTRAL" && !PlayableClasses.Contains(c)) ||
                (classes.Contains("NEUTRAL") && classes.Length != 1))
                throw new ArgumentException("Pool requirement needs printed playable classes or NEUTRAL.", nameof(classDomain));
            if (!Enum.IsDefined(typeof(PoolCostBinding), costBinding) ||
                (costBinding == PoolCostBinding.None) == ordinaryCost.HasValue)
                throw new ArgumentException("Dynamic pool binding requires exactly one ordinary cost.", nameof(costBinding));
            if (allowThinExactCost && costBinding != PoolCostBinding.StackNumber &&
                !(costBinding == PoolCostBinding.None && filter.MinCost.HasValue && filter.MinCost == filter.MaxCost))
                throw new ArgumentException("Thin-tier exemption requires exact printed Cost.", nameof(allowThinExactCost));
            if (allowUnimplementedCuratedCards && (costBinding != PoolCostBinding.None || allowThinExactCost ||
                filter.CardIds == null || filter.CardIds.Count < OfflinePoolAdmission.MinimumCandidates ||
                filter != new OfflinePoolFilter(type: filter.Type, cardIds: filter.CardIds)))
                throw new ArgumentException("Missing-card exemption requires at least three fixed Standard identities with only an optional type.", nameof(allowUnimplementedCuratedCards));
            AllowThinExactCost = allowThinExactCost;
            AllowUnimplementedCuratedCards = allowUnimplementedCuratedCards;
            Name = name; Filter = filter; Destination = destination; ClassDomain = Array.AsReadOnly(classes);
            CostBinding = costBinding; OrdinaryCost = ordinaryCost;
        }

        internal IEnumerable<string> Classes => ClassDomain.Contains("NEUTRAL") ? PlayableClasses : ClassDomain;
        internal static CardClass HeroClass(string name) => name == "DEMONHUNTER" ? (CardClass)14 : (CardClass)Enum.Parse(typeof(CardClass), name);
    }

    public sealed class OfflinePoolDeclaration
    {
        public string CardId { get; }
        public int DbfId { get; }
        public IReadOnlyList<PoolRequirement> Pools { get; }
        public OfflinePoolDeclaration(string cardId, int dbfId, params PoolRequirement[] pools)
        {
            if (string.IsNullOrWhiteSpace(cardId) || dbfId <= 0) throw new ArgumentException("Invalid pool source identity.");
            if (pools == null || pools.Length == 0 || pools.Any(p => p == null) || pools.Select(p => p.Name).Distinct().Count() != pools.Length)
                throw new ArgumentException("Source needs distinct named pool requirements.", nameof(pools));
            CardId = cardId; DbfId = dbfId; Pools = Array.AsReadOnly(pools.ToArray());
        }
    }
}
