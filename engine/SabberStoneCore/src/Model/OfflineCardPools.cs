using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;

namespace SabberStoneCore.Model
{
    public static class OfflineCardPools
    {
        private static readonly CardClass[] PlayableClasses = PoolRequirement.PlayableClasses.Select(PoolRequirement.HeroClass).ToArray();
        private static readonly int PlayableClassMask = PlayableClasses.Aggregate(0, (mask, c) => mask | 1 << ((int)c - 1));
        private static readonly ConcurrentDictionary<(string, CardClass, OfflinePoolFilter, int, PoolDestination, bool, int), IReadOnlyList<Card>> Cache =
            new ConcurrentDictionary<(string, CardClass, OfflinePoolFilter, int, PoolDestination, bool, int), IReadOnlyList<Card>>();

        public static int PrintedClassMask(Card source)
        {
            if (source == null || source.Class == CardClass.NEUTRAL && source[GameTag.MULTIPLE_CLASSES] == 0 && source.MultiClassGroup == 0)
                return 0;
            int mask = 0;
            foreach (var c in PlayableClasses)
                if (CardClassRules.IsAllowedInDeck(source, c)) mask |= 1 << ((int)c - 1);
            return mask;
        }

        public static IReadOnlyList<Card> Candidates(OfflinePoolManifest manifest, CardClass heroClass,
            OfflinePoolFilter filter, int sourceDbfId, PoolDestination destination = PoolDestination.Hand, int? sourceClassMask = null) =>
            Candidates(manifest, heroClass, filter, sourceDbfId, destination, true, sourceClassMask);

        private static IReadOnlyList<Card> Candidates(OfflinePoolManifest manifest, CardClass heroClass,
            OfflinePoolFilter filter, int sourceDbfId, PoolDestination destination, bool allowDiscoverFallback, int? sourceClassMask = null)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (!Enum.IsDefined(typeof(PoolDestination), destination)) throw new ArgumentOutOfRangeException(nameof(destination));
            // Runtime definitions may keep another card's class under a neutral generated-token ID.
            int classes = allowDiscoverFallback && filter.DiscoverFallback && filter.ClassMode == PoolClassMode.ControllerAndNeutral
                ? (sourceClassMask ?? PrintedClassMask(Cards.FromAssetId(sourceDbfId))) & PlayableClassMask : 0;
            return Cache.GetOrAdd((manifest.Hash, heroClass, filter, sourceDbfId, destination, allowDiscoverFallback, classes), _ => {
                var eligible = (filter.Scope == PoolScope.Wild ? manifest.Standard.Concat(manifest.Past).OrderBy(m => m.DbfId)
                    : filter.Scope == PoolScope.Standard ? (IEnumerable<OfflinePoolPolicy.Member>)manifest.Standard : manifest.Past)
                .Where(m => m.DbfId != sourceDbfId &&
                    // Colossal bodies may be generated off-board, never selected for a direct summon/transform.
                    (!m.Tags.TryGetValue(2247, out int colossal) || colossal <= 0 ||
                        (destination == PoolDestination.Hand || destination == PoolDestination.Deck) &&
                        (!m.Tags.TryGetValue(4538, out int herald) || herald <= 0))).ToArray();
                var selected = eligible.Where(m => Matches(m, heroClass, filter)).ToArray();
                if (selected.Length == 0 && allowDiscoverFallback && filter.DiscoverFallback &&
                    filter.ClassMode == PoolClassMode.ControllerAndNeutral)
                {
                    // Unpinned empty-pool fallback; never fill a nonempty hero pool with source-class cards.
                    selected = eligible.Where(m => PlayableClasses.Any(c => (classes & 1 << ((int)c - 1)) != 0 && Matches(m, c, filter))).ToArray();
                }
                return Array.AsReadOnly(selected.Select(OfflinePoolManifest.Definition).ToArray());
            });
        }

        private static OfflinePoolManifest Manifest(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (game.LegacyPoolReplay) throw new InvalidOperationException("Offline pools are unavailable in schema-2 legacy replay mode.");
            return game.OfflinePools ?? throw new InvalidOperationException("Offline pools require StartGame.");
        }

        public static IReadOnlyList<Card> Candidates(Game game, CardClass heroClass,
            OfflinePoolFilter filter, int sourceDbfId, PoolDestination destination = PoolDestination.Hand, int? sourceClassMask = null) =>
            Candidates(Manifest(game), heroClass, filter, sourceDbfId, destination, sourceClassMask);

        public static Card[] SampleDistinct(Game game, OfflinePoolFilter filter, CardClass heroClass, int sourceDbfId, PoolDestination destination = PoolDestination.Hand, int? sourceClassMask = null) =>
            SampleDistinct(Manifest(game), game.Random, filter, heroClass, sourceDbfId, destination, sourceClassMask);

        public static Card Pick(Game game, OfflinePoolFilter filter, CardClass heroClass, int sourceDbfId, PoolDestination destination = PoolDestination.Hand) =>
            Pick(Manifest(game), game.Random, filter, heroClass, sourceDbfId, destination);

        private static bool Matches(OfflinePoolPolicy.Member m, CardClass heroClass, OfflinePoolFilter f)
        {
            string hero = ClassName(heroClass);
            bool classMatch = f.ClassMode == PoolClassMode.Any ||
                (f.ClassMode == PoolClassMode.ControllerAndNeutral && (m.Classes.Contains(hero) || m.Classes.Contains("NEUTRAL"))) ||
                (f.ClassMode == PoolClassMode.OtherClass && !m.Classes.Contains(hero) && !m.Classes.Contains("NEUTRAL")) ||
                (f.ClassMode == PoolClassMode.Exact && m.Classes.Contains(ClassName(f.ExactClass.Value)));
            bool oldGod = m.Tags.TryGetValue(4754,out int oldGodTag) && oldGodTag > 0;
            return (!f.ExactAttack.HasValue || m.Attack == f.ExactAttack.Value) &&
                (!f.AttackParity.HasValue || m.Attack % 2 == f.AttackParity.Value) &&
                (!f.CostParity.HasValue || m.Cost % 2 == f.CostParity.Value) &&
                (f.CardIds == null || f.CardIds.Contains(m.CardId)) &&
                (f.AnyOf.Count == 0 || f.AnyOf.Any(branch => Matches(m, heroClass, branch))) && (!oldGod || f.Keyword == (GameTag)4754 || f.CardIds?.Contains(m.CardId) == true) && classMatch && (!f.Type.HasValue || m.Type == f.Type.Value.ToString()) &&
                (!f.MinCost.HasValue || m.Cost >= f.MinCost.Value) && (!f.MaxCost.HasValue || m.Cost <= f.MaxCost.Value) &&
                (!f.Race.HasValue || IsRace(m, f.Race.Value)) &&
                (!f.MultipleMinionTypes || m.Races.Count > 1 || m.Races.Contains("ALL")) &&
                (!f.SpellSchool.HasValue || School(m.SpellSchool) == f.SpellSchool.Value) &&
                (!f.Rarity.HasValue || m.Rarity == f.Rarity.Value.ToString()) &&
                (!f.Keyword.HasValue || (m.Tags.TryGetValue((int)f.Keyword.Value, out int value) && value > 0));
        }

        // Native class 14 has no named value in the legacy engine enum.
        private static string ClassName(CardClass value) => (int)value == 14 ? "DEMONHUNTER" : value.ToString();
        private static int School(string school)
        {
            switch (school)
            {
                case "NONE": return 0; case "ARCANE": return 1; case "FIRE": return 2;
                case "FROST": return 3; case "NATURE": return 4; case "HOLY": return 5;
                case "SHADOW": return 6; case "FEL": return 7;
                default: throw new InvalidOperationException("Unknown catalog spell school: " + school);
            }
        }
        private static bool IsRace(OfflinePoolPolicy.Member member, Race race)
        {
            // Same ALL/minion-type semantics as Card.IsRace, using pinned catalog metadata.
            if (race == Race.ALL) return member.Races.Contains("ALL");
            switch (race)
            {
                case Race.DRAENEI: case Race.UNDEAD: case Race.MURLOC: case Race.DEMON:
                case Race.MECHANICAL: case Race.ELEMENTAL: case Race.BEAST: case Race.TOTEM:
                case Race.PIRATE: case Race.DRAGON: case Race.QUILBOAR: case Race.NAGA:
                    return member.Races.Contains("ALL") || member.Races.Contains(race.ToString());
                default: return false;
            }
        }

        internal static Card[] SampleDistinct(OfflinePoolManifest manifest, Util.DeepCloneableRandom random,
            OfflinePoolFilter filter, CardClass heroClass, int sourceDbfId, PoolDestination destination = PoolDestination.Hand, int? sourceClassMask = null)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var candidates = Candidates(manifest, heroClass, filter, sourceDbfId, destination, sourceClassMask);
            if (candidates.Count <= 3) return candidates.ToArray();
            var indices = Enumerable.Range(0, candidates.Count).ToArray();
            var result = new Card[3];
            for (int i = 0; i < result.Length; i++)
            {
                // Existing Game.Random API: Next(bound) consumes exactly one primitive draw.
                int j = i + random.Next(indices.Length - i);
                int swap = indices[i]; indices[i] = indices[j]; indices[j] = swap;
                result[i] = candidates[indices[i]];
            }
            return result;
        }

        internal static Card Pick(OfflinePoolManifest manifest, Util.DeepCloneableRandom random,
            OfflinePoolFilter filter, CardClass heroClass, int sourceDbfId, PoolDestination destination = PoolDestination.Hand)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var candidates = Candidates(manifest, heroClass, filter, sourceDbfId, destination, false);
            return candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
        }
    }
}
