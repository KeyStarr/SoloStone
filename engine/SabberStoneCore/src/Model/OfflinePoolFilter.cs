using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;

namespace SabberStoneCore.Model
{
    public enum PoolScope { Standard, Past, Wild }
    public enum PoolDestination { Hand, Deck, Summon, Transform, Other }
    public enum PoolClassMode { Any, ControllerAndNeutral, OtherClass, Exact }

    /// <summary>Only resolved values belong here; bind dynamic costs at effect resolution.</summary>
    public readonly struct OfflinePoolFilter : IEquatable<OfflinePoolFilter>
    {
        public PoolScope Scope { get; }
        public PoolClassMode ClassMode { get; }
        public bool DiscoverFallback { get; }
        public CardClass? ExactClass { get; }
        public CardType? Type { get; }
        public int? MinCost { get; }
        public int? MaxCost { get; }
        public Race? Race { get; }
        public bool MultipleMinionTypes { get; }
        public int? SpellSchool { get; }
        public Rarity? Rarity { get; }
        public GameTag? Keyword { get; }
        public int? ExactAttack { get; }
        public int? AttackParity { get; }
        public int? CostParity { get; }
        private readonly IReadOnlyList<string> _cardIds;
        private readonly IReadOnlyList<OfflinePoolFilter> _anyOf;
        public IReadOnlyList<string> CardIds => _cardIds;
        public IReadOnlyList<OfflinePoolFilter> AnyOf => _anyOf ?? Array.Empty<OfflinePoolFilter>();

        public OfflinePoolFilter(PoolScope scope = PoolScope.Standard, PoolClassMode classMode = PoolClassMode.Any,
            CardClass? exactClass = null, CardType? type = null, int? minCost = null, int? maxCost = null,
            Race? race = null, bool multipleMinionTypes = false, int? spellSchool = null,
            Rarity? rarity = null, GameTag? keyword = null, int? exactAttack = null,
            int? attackParity = null, int? costParity = null, IEnumerable<string> cardIds = null,
            IEnumerable<OfflinePoolFilter> anyOf = null, bool discoverFallback = false)
        {
            if (!Enum.IsDefined(typeof(PoolScope), scope) || !Enum.IsDefined(typeof(PoolClassMode), classMode))
                throw new ArgumentOutOfRangeException(nameof(scope));
            if ((classMode == PoolClassMode.Exact) != exactClass.HasValue)
                throw new ArgumentException("Exact class is required only for Exact mode.", nameof(exactClass));
            if (attackParity.HasValue && attackParity != 0 && attackParity != 1 ||
                costParity.HasValue && costParity != 0 && costParity != 1)
                throw new ArgumentOutOfRangeException(nameof(attackParity), "Parity must be zero (even) or one (odd).");
            var ids = cardIds?.ToArray();
            if (ids != null && ids.Any(id => string.IsNullOrWhiteSpace(id) || id.Contains(",")))
                throw new ArgumentException("Curated pools require exact card identities.", nameof(cardIds));
            _cardIds = ids == null ? null : Array.AsReadOnly(ids.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray());
            var branches = anyOf?.Distinct().ToArray();
            if (branches != null && (branches.Length == 0 || branches.Any(f => f.Scope != scope)))
                throw new ArgumentException("Union branches must be nonempty and share the root scope.", nameof(anyOf));
            _anyOf = branches == null ? null : Array.AsReadOnly(branches);
            ExactAttack = exactAttack; AttackParity = attackParity; CostParity = costParity;
            Scope = scope; ClassMode = classMode; ExactClass = exactClass; Type = type;
            DiscoverFallback = discoverFallback;
            MinCost = minCost; MaxCost = maxCost; Race = race; MultipleMinionTypes = multipleMinionTypes;
            SpellSchool = spellSchool; Rarity = rarity; Keyword = keyword;
        }

        public static OfflinePoolFilter Discover(CardType? type = null, int? minCost = null, int? maxCost = null,
            Race? race = null, bool multipleMinionTypes = false, int? spellSchool = null,
            Rarity? rarity = null, GameTag? keyword = null, PoolScope scope = PoolScope.Standard) =>
            new OfflinePoolFilter(scope, PoolClassMode.ControllerAndNeutral, type: type, minCost: minCost,
                maxCost: maxCost, race: race, multipleMinionTypes: multipleMinionTypes, spellSchool: spellSchool,
                rarity: rarity, keyword: keyword, discoverFallback: true);

        public static OfflinePoolFilter Random(CardType? type = null, int? minCost = null, int? maxCost = null,
            Race? race = null, bool multipleMinionTypes = false, int? spellSchool = null,
            Rarity? rarity = null, GameTag? keyword = null, PoolScope scope = PoolScope.Standard) =>
            new OfflinePoolFilter(scope, type: type, minCost: minCost, maxCost: maxCost, race: race,
                multipleMinionTypes: multipleMinionTypes, spellSchool: spellSchool, rarity: rarity, keyword: keyword);

        public static OfflinePoolFilter Union(params OfflinePoolFilter[] filters)
        {
            if (filters == null || filters.Length == 0) throw new ArgumentException("Union requires filters.", nameof(filters));
            return new OfflinePoolFilter(scope: filters[0].Scope, anyOf: filters);
        }

        public OfflinePoolFilter WithCost(int cost) => new OfflinePoolFilter(Scope, ClassMode, ExactClass, Type,
            cost, cost, Race, MultipleMinionTypes, SpellSchool, Rarity, Keyword, ExactAttack, AttackParity, CostParity, CardIds, _anyOf, DiscoverFallback);

        public OfflinePoolFilter WithMaxCost(int cost) => new OfflinePoolFilter(Scope, ClassMode, ExactClass, Type,
            MinCost, cost, Race, MultipleMinionTypes, SpellSchool, Rarity, Keyword, ExactAttack, AttackParity, CostParity, CardIds, _anyOf, DiscoverFallback);

        private (PoolScope, PoolClassMode, CardClass?, CardType?, int?, int?, Race?, bool, int?, Rarity?, GameTag?) Key =>
            (Scope, ClassMode, ExactClass, Type, MinCost, MaxCost, Race, MultipleMinionTypes, SpellSchool, Rarity, Keyword);
        public bool Equals(OfflinePoolFilter other) => Key.Equals(other.Key) && DiscoverFallback == other.DiscoverFallback && ExactAttack == other.ExactAttack
            && AttackParity == other.AttackParity && CostParity == other.CostParity
            && (_cardIds == null ? other._cardIds == null : other._cardIds != null && _cardIds.SequenceEqual(other._cardIds))
            && AnyOf.SequenceEqual(other.AnyOf);
        public override bool Equals(object obj) => obj is OfflinePoolFilter other && Equals(other);
        public override int GetHashCode()
        {
            // ValueTuple with more than eight fields hashes only its trailing fields.
            unchecked
            {
                int hash = (int)Scope;
                hash = hash * 31 + (int)ClassMode; hash = hash * 31 + ExactClass.GetHashCode();
                hash = hash * 31 + Type.GetHashCode(); hash = hash * 31 + MinCost.GetHashCode();
                hash = hash * 31 + MaxCost.GetHashCode(); hash = hash * 31 + Race.GetHashCode();
                hash = hash * 31 + MultipleMinionTypes.GetHashCode(); hash = hash * 31 + SpellSchool.GetHashCode();
                hash = hash * 31 + Rarity.GetHashCode(); hash = hash * 31 + Keyword.GetHashCode();
                // Preserve existing filter hashes when all extension predicates are absent.
                if (ExactAttack.HasValue) hash = hash * 31 + ExactAttack.Value + 1;
                if (AttackParity.HasValue) hash = hash * 31 + AttackParity.Value + 3;
                if (CostParity.HasValue) hash = hash * 31 + CostParity.Value + 5;
                if (_cardIds != null)
                {
                    hash = hash * 31 + 7;
                    foreach (string id in _cardIds) hash = hash * 31 + StringComparer.Ordinal.GetHashCode(id);
                }
                foreach (var branch in AnyOf) hash = hash * 31 + branch.GetHashCode();
                if (DiscoverFallback) hash = hash * 31 + 11;
                return hash;
            }
        }
        public static bool operator ==(OfflinePoolFilter left, OfflinePoolFilter right) => left.Equals(right);
        public static bool operator !=(OfflinePoolFilter left, OfflinePoolFilter right) => !left.Equals(right);
    }
}
