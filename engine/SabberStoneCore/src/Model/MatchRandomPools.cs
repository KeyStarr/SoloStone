using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Model.RandomPools
{
    // Not in the closed snapshot schema's namespace list: this is derived state, never serialized.
    /// <summary>
    /// The card pool a match's legacy random effects (random minion, random card, random spell, Discover) draw from.
    /// Normal mode is built from the match's pinned offline pool manifest with the current card values, so a
    /// Standard-policy game only ever generates Standard cards. Legacy mode (schema-2 replays and records written
    /// before the policy was recorded) hands out the very same frozen list objects as before, so those recordings
    /// stay byte-identical. The pool key is <see cref="GameConfig.PoolFormat"/>, never <see cref="Game.FormatType"/>.
    /// </summary>
    public sealed class MatchRandomPool
    {
        /// <summary>Recorded in new game records; a record without it replays on the frozen legacy lists.</summary>
        public const string PolicyVersion = "match-random/1";

        // Diagnostics only: (source card id or null, legacy?) for every accessor call. Tests and fuzz census set it.
        public static Action<string, bool> Census { get; set; }

        // Hook for the Wild pool: cards that Wild adds on top of Standard. Empty until Wild pools exist.
        // Never Cards.AllWild or the frozen legacy lists: those hold stub cards from unimplemented sets.
        internal static IReadOnlyList<OfflinePoolPolicy.Member> WildExtra(OfflinePoolManifest manifest) =>
            Array.Empty<OfflinePoolPolicy.Member>();

        private sealed class Slot { public readonly ConcurrentDictionary<(GenerationFormat, PoolDestination), MatchRandomPool> Pools =
            new ConcurrentDictionary<(GenerationFormat, PoolDestination), MatchRandomPool>(); }
        private static readonly ConditionalWeakTable<OfflinePoolManifest, Slot> PerManifest = new ConditionalWeakTable<OfflinePoolManifest, Slot>();
        private static readonly MatchRandomPool LegacyStandard = new MatchRandomPool(FormatType.FT_STANDARD);
        private static readonly MatchRandomPool LegacyWild = new MatchRandomPool(FormatType.FT_WILD);

        private readonly FormatType _legacyFormat;
        private readonly Card[] _all;
        private readonly OfflinePoolPolicy.Member[] _members;
        private readonly Dictionary<int, List<Card>> _costMinions;
        private readonly ConcurrentDictionary<CardClass, Card[]> _classOnly = new ConcurrentDictionary<CardClass, Card[]>();
        private readonly ConcurrentDictionary<CardClass, Card[]> _classAndNeutral = new ConcurrentDictionary<CardClass, Card[]>();
        private readonly ConcurrentDictionary<CardClass, Card[]> _secrets = new ConcurrentDictionary<CardClass, Card[]>();
        private readonly ConcurrentDictionary<CardClass, Card[]> _nonClass = new ConcurrentDictionary<CardClass, Card[]>();
        private readonly Dictionary<string, OfflinePoolPolicy.Member> _byId;

        // Discover's criteria pools, kept ON this pool so they live and die with their manifest and policy
        // (a process-wide cache once leaked the first game's pool into later games).
        internal ConcurrentDictionary<(object Criteria, CardClass Class), Card[][]> DiscoverMemo { get; } =
            new ConcurrentDictionary<(object, CardClass), Card[][]>();

        /// <summary>True when this is the frozen legacy list set (same objects as Cards.AllStandard and friends).</summary>
        public bool Legacy { get; }
        public GenerationFormat Format { get; }
        /// <summary>Legacy mode only: the FormatType whose frozen lists this pool hands out.</summary>
        internal FormatType LegacyFormat => _legacyFormat;
        public PoolDestination Destination { get; }
        public string ManifestHash { get; }

        private MatchRandomPool(FormatType legacyFormat)
        {
            Legacy = true; _legacyFormat = legacyFormat; ManifestHash = "legacy";
        }

        private MatchRandomPool(OfflinePoolManifest manifest, GenerationFormat format, PoolDestination destination)
        {
            Format = format; Destination = destination; ManifestHash = manifest.Hash;
            var members = new List<OfflinePoolPolicy.Member>();
            var cards = new List<Card>();
            IEnumerable<OfflinePoolPolicy.Member> source = manifest.Standard;
            if (format == GenerationFormat.Wild) source = source.Concat(WildExtra(manifest));
            foreach (var m in source)
            {
                // Hero cards never belong to a random pool (legacy pools excluded them too). Neither did they hold
                // locations: each task keeps its current card-type behaviour (owner ruling, 2026-11-16).
                if (m.Type == "HERO" || m.Type == "LOCATION") continue;
                // Colossal bodies may be generated off-board, never selected for a direct summon/transform;
                // a Herald Colossal is rejected from every pool (same rules as OfflineCardPools.Candidates).
                if (m.Tags.TryGetValue(2247, out int colossal) && colossal > 0 &&
                    !((destination == PoolDestination.Hand || destination == PoolDestination.Deck) &&
                      (!m.Tags.TryGetValue(4538, out int herald) || herald <= 0))) continue;
                // Old Gods are left out of ordinary pools.
                if (m.Tags.TryGetValue(4754, out int oldGod) && oldGod > 0) continue;
                var card = OfflinePoolManifest.Definition(m);
                if (card == null || !card.Implemented) continue;
                // Retail 26.0.4: cards costing three runes of any mix are never generated.
                if (card[GameTag.COST_BLOOD] + card[GameTag.COST_FROST] + card[GameTag.COST_UNHOLY] >= 3) continue;
                members.Add(m); cards.Add(card);
            }
            _members = members.ToArray(); _all = cards.ToArray();
            _byId = new Dictionary<string, OfflinePoolPolicy.Member>(StringComparer.Ordinal);
            for (int i = 0; i < _all.Length; i++) _byId[_all[i].Id] = _members[i];
            _costMinions = new Dictionary<int, List<Card>>();
            // Buckets follow the CURRENT card cost, not the frozen legacy buckets.
            foreach (var card in _all)
            {
                if (card.Type != CardType.MINION) continue;
                if (!_costMinions.TryGetValue(card.Cost, out var list)) _costMinions[card.Cost] = list = new List<Card>();
                list.Add(card);
            }
        }

        public static MatchRandomPool For(Game game, PoolDestination destination = PoolDestination.Other, IEntity source = null)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            bool legacy = game.LegacyPoolReplay || game.UnversionedRandomPoolReplay;
            Census?.Invoke(source?.Card?.Id, legacy);
            if (legacy) return game.FormatType == FormatType.FT_STANDARD ? LegacyStandard : LegacyWild;
            var manifest = game.OfflinePools ?? OfflinePoolManifest.Installed;
            var slot = PerManifest.GetValue(manifest, _ => new Slot());
            return slot.Pools.GetOrAdd((game.PoolFormat, destination), key => new MatchRandomPool(manifest, key.Item1, key.Item2));
        }

        /// <summary>Every generatable card (the AllStandard shape): no heroes.</summary>
        public IReadOnlyList<Card> All => Legacy
            ? (IReadOnlyList<Card>)(_legacyFormat == FormatType.FT_STANDARD ? Cards.AllStandard : Cards.AllWild)
            : _all;

        /// <summary>Minions with exactly this CURRENT cost. Legacy mode indexes the frozen bucket and throws on a missing cost, as before.</summary>
        public List<Card> MinionsOfCost(int cost)
        {
            if (Legacy) return Cards.CostMinionCards(_legacyFormat)[cost];
            return _costMinions.TryGetValue(cost, out var list) ? list : new List<Card>();
        }

        public bool TryMinionsOfCost(int cost, out List<Card> minions)
        {
            if (Legacy) return Cards.CostMinionCards(_legacyFormat).TryGetValue(cost, out minions);
            return _costMinions.TryGetValue(cost, out minions);
        }

        /// <summary>Cards for a deck of this class: the class's cards plus neutrals (the Standard[class] shape).</summary>
        public IReadOnlyList<Card> ClassAndNeutral(CardClass heroClass)
        {
            if (Legacy) return Cards.FormatTypeClassCards(_legacyFormat)[heroClass];
            return _classAndNeutral.GetOrAdd(heroClass, c =>
            {
                string hero = OfflineCardPools.ClassName(c);
                return Pick(m => m.Classes.Contains(hero) || m.Classes.Contains("NEUTRAL"));
            });
        }

        /// <summary>Cards that list this class, a multi-class card counting for each class it lists (new mode only).</summary>
        public IReadOnlyList<Card> ClassOnly(CardClass cardClass)
        {
            if (Legacy) throw new NotSupportedException("ClassOnly is a new-mode accessor; legacy sites keep their frozen predicate.");
            return _classOnly.GetOrAdd(cardClass, c =>
            {
                string name = OfflineCardPools.ClassName(c);
                return Pick(m => m.Classes.Contains(name));
            });
        }

        /// <summary>New mode: the neutral side of a class pool (class+neutral cards that do not list the class).</summary>
        public IReadOnlyList<Card> NonClass(CardClass heroClass)
        {
            if (Legacy) throw new NotSupportedException("NonClass is a new-mode accessor.");
            return _nonClass.GetOrAdd(heroClass, c =>
            {
                string name = OfflineCardPools.ClassName(c);
                return Pick(m => !m.Classes.Contains(name) && m.Classes.Contains("NEUTRAL"));
            });
        }

        /// <summary>New mode: does the card list this class (a multi-class card counts for each class it lists)?</summary>
        public bool InClass(Card card, CardClass cardClass)
        {
            if (Legacy) throw new NotSupportedException("InClass is a new-mode accessor.");
            return _byId.TryGetValue(card.Id, out var m) && m.Classes.Contains(OfflineCardPools.ClassName(cardClass));
        }

        /// <summary>Secrets of the class pool, cached on this pool (new mode) or in the frozen legacy memo.</summary>
        public IReadOnlyList<Card> Secrets(CardClass cardClass)
        {
            if (Legacy) throw new NotSupportedException("Legacy secrets use the frozen format-keyed memo.");
            return _secrets.GetOrAdd(cardClass, c => ClassAndNeutral(c).Where(card => card.IsSecret).ToArray());
        }

        /// <summary>New mode: a generated card never rolls its own source (modern code's rule). Legacy keeps the old behaviour.</summary>
        public IReadOnlyList<Card> WithoutSource(IReadOnlyList<Card> cards, IEntity source)
        {
            if (Legacy || source?.Card == null) return cards;
            int id = source.Card.AssetId;
            for (int i = 0; i < cards.Count; i++)
                if (cards[i].AssetId == id) return cards.Where(c => c.AssetId != id).ToArray();
            return cards;
        }

        private Card[] Pick(Func<OfflinePoolPolicy.Member, bool> test)
        {
            var result = new List<Card>();
            for (int i = 0; i < _all.Length; i++) if (test(_members[i])) result.Add(_all[i]);
            return result.ToArray();
        }

        /// <summary>The member metadata behind a new-mode pool, for proofs.</summary>
        internal IReadOnlyList<OfflinePoolPolicy.Member> Members => _members;
    }
}
