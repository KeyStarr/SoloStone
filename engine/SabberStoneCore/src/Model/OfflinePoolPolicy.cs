using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.src.Loader;

namespace SabberStoneCore.Model
{
    /// <summary>Pinned admission catalog only. Loading does not initialize Cards or consume RNG.</summary>
    public sealed class OfflinePoolPolicy
    {
        private static readonly string[] StandardSets = { "EMERALD_DREAM", "THE_LOST_CITY", "TIME_TRAVEL",
            "CATACLYSM", "ESCAPEFROM_VIOLET_HOLD", "BE", "CORE" };
        private static readonly Lazy<OfflinePoolPolicy> Installed = new Lazy<OfflinePoolPolicy>(() => {
            using (var stream = typeof(OfflinePoolPolicy).Assembly.GetManifestResourceStream("SabberStoneCore.Resources.OfflinePoolCatalog.xml"))
            {
                if (stream == null) throw new InvalidDataException("Missing offline pool catalog");
                return Parse(XDocument.Load(stream));
            }
        });

        public string PolicyVersion { get; }
        public string ClientBuild { get; }
        [field: NonSerialized] public string ActiveMetaId { get; }
        [field: NonSerialized] public IReadOnlyList<Meta> Metas { get; }
        [NonSerialized] private readonly HashSet<int> inactiveSetIds;
        public IReadOnlyList<string> Sets { get; }
        public IReadOnlyList<string> PackEligibleSets => Array.AsReadOnly(Sets.Where(s => s != "CORE").ToArray());
        public IReadOnlyList<string> ExtraCards => Metas.SingleOrDefault(m => m.Id == ActiveMetaId)?.ExtraCards ?? Array.Empty<string>();
        public string NewestSet => Metas.SingleOrDefault(m => m.Id == ActiveMetaId)?.NewestSet;
        public IReadOnlyList<Member> Members { get; }
        public IReadOnlyList<Dependency> Dependencies { get; }
        public IReadOnlyDictionary<string, string> SourceHashes { get; }

        private OfflinePoolPolicy(string version, string build, string[] sets, Member[] members,
            Dependency[] dependencies, Dictionary<string, string> hashes, string activeMetaId, Meta[] metas)
        {
            PolicyVersion = version; ClientBuild = build;
            ActiveMetaId = activeMetaId; Metas = Array.AsReadOnly(metas);
            inactiveSetIds = new HashSet<int>(metas.SelectMany(m => m.SetIds)
                .Where(pair => !sets.Contains(pair.Key)).Select(pair => pair.Value));
            Sets = Array.AsReadOnly(sets); Members = Array.AsReadOnly(members);
            Dependencies = Array.AsReadOnly(dependencies);
            SourceHashes = new ReadOnlyDictionary<string, string>(hashes);
        }

        public static OfflinePoolPolicy Load() => Installed.Value;

        // Historical Classic catalogs remain supported; known future sets never become Past cards.
        public bool AllowsSet(int setId) => !inactiveSetIds.Contains(setId);
        public bool AllowsCard(Card card) => AllowsCard(card, Sets);
        internal bool AllowsCard(Card card, IReadOnlyList<string> sets)
        {
            if (card == null) return false;
            var meta = Metas.SingleOrDefault(m => m.Sets.SequenceEqual(sets));
            if (meta != null && (meta.SetIds.Values.Contains((int)card.Set) || meta.ExtraCards.Contains(card.Id)))
                return true;
            if (Metas.Any(m => m.ExtraCards.Contains(card.Id) || m.ExtraCardSetIds.Values.Contains((int)card.Set)))
                return meta != null && meta.ExtraCards.Contains(card.Id);
            return !Metas.SelectMany(m => m.SetIds).Any(pair => pair.Value == (int)card.Set && !sets.Contains(pair.Key));
        }

        public bool IsPackEligible(Card card) => card != null && !Metas.Any(m => m.ExtraCards.Contains(card.Id))
            && (Metas.SingleOrDefault(m => m.Id == ActiveMetaId)?.SetIds.Any(p => p.Key != "CORE" && p.Value == (int)card.Set)
                ?? Members.Any(m => m.CardId == card.Id && PackEligibleSets.Contains(m.Set)));

        public sealed class Meta
        {
            public string Id { get; }
            public string Label { get; }
            public IReadOnlyList<string> Sets { get; }
            public string NewestSet { get; }
            public IReadOnlyList<string> ExtraCards { get; }
            internal IReadOnlyDictionary<string, int> ExtraCardSetIds { get; }
            internal IReadOnlyDictionary<string, int> SetIds { get; }
            internal Meta(XElement row)
            {
                Id = Required(row, "id"); Label = Required(row, "label");
                Sets = Strings(row, "Set", "name");
                if (Sets.Count == 0) throw new InvalidDataException("Empty meta sets");
                NewestSet = Required(row, "newestSet");
                if (NewestSet == "CORE" || !Sets.Contains(NewestSet)) throw new InvalidDataException("Invalid meta newestSet");
                ExtraCards = Strings(row, "ExtraCard", "cardId");
                ExtraCardSetIds = new ReadOnlyDictionary<string, int>(row.Elements("ExtraCard")
                    .ToDictionary(e => Required(e, "cardId"), e => Number(e, "setId", 1)));
                if (row.Elements("ExtraCard").Any(e => new[] { "INVALID", "UNKNOWN" }.Contains(Required(e, "set"))
                    || Sets.Contains(Required(e, "set")))) throw new InvalidDataException("Extra card belongs to meta sets or has unresolved set");
                var ids = row.Elements("Set").ToDictionary(e => Required(e, "name"), e => Number(e, "id", 1));
                if (ids.Values.Distinct().Count() != ids.Count) throw new InvalidDataException("Duplicate meta set identity");
                SetIds = new ReadOnlyDictionary<string, int>(ids);
            }
        }

        public OfflinePoolManifest BuildManifest()
        {
            ValidateRegistration();
            return new OfflinePoolManifest(this, PolicyVersion, OfflinePoolManifest.CurrentSamplingVersion);
        }

        // Unpinned offline collation; see native-tail/avatar-standard-pack.yaml.
        public static OfflinePoolFilter StandardPackFilter(Rarity rarity, OfflinePoolManifest manifest = null)
        {
            var sets = manifest == null ? Load().PackEligibleSets : manifest.PackEligibleSets;
            return new OfflinePoolFilter(rarity: rarity,
                cardIds: (manifest == null ? Load().Members.Where(m => m.Scope == "Standard") : manifest.Standard)
                    .Where(m => sets.Contains(m.Set)).Select(m => m.CardId));
        }

        public static Card[] OpenStandardPack(Game game, int sourceDbfId)
        {
            var result = new List<Card>(5);
            for (int slot = 0; slot < 5; slot++)
            {
                int roll = game.Random.Next(100);
                Rarity rarity = slot == 0
                    ? roll < 75 ? Rarity.RARE : roll < 95 ? Rarity.EPIC : Rarity.LEGENDARY
                    : roll < 75 ? Rarity.COMMON : roll < 95 ? Rarity.RARE : roll < 99 ? Rarity.EPIC : Rarity.LEGENDARY;
                var card = OfflineCardPools.Pick(game, StandardPackFilter(rarity, game.OfflinePools), CardClass.NEUTRAL, sourceDbfId);
                if (card != null) result.Add(card);
            }
            return result.ToArray();
        }

        public sealed class Dependency
        {
            public int DbfId { get; }
            public string CardId { get; }
            public IReadOnlyList<string> DependencyIds { get; }
            internal Dependency(XElement row)
            {
                DbfId = Number(row, "dbfId", 1); CardId = Required(row, "cardId");
                DependencyIds = Strings(row, "Dependency", "cardId");
            }
        }

        public sealed class Member
        {
            public int DbfId { get; }
            public string CardId { get; }
            public string Scope { get; }
            public string Set { get; }
            public string ProofKey { get; }
            public string RegistrationKind { get; }
            public string Type { get; }
            public int Cost { get; }
            public int Attack { get; }
            public int Health { get; }
            public string Rarity { get; }
            public string SpellSchool { get; }
            public IReadOnlyList<string> Classes { get; }
            public IReadOnlyList<string> Races { get; }
            public IReadOnlyList<string> DependencyIds { get; }
            public IReadOnlyDictionary<int, int> Tags { get; }
            internal Member(XElement row)
            {
                DbfId = Number(row, "dbfId", 1); CardId = Required(row, "cardId");
                Scope = Required(row, "scope"); Set = Required(row, "set");
                ProofKey = Required(row, "proofKey"); RegistrationKind = Required(row, "registrationKind");
                Type = Required(row, "type"); Cost = Number(row, "cost", 0);
                Attack = Number(row, "attack", 0); Health = Number(row, "health", 0);
                Rarity = Required(row, "rarity"); SpellSchool = Required(row, "spellSchool");
                Classes = Strings(row, "Class", "name"); Races = Strings(row, "Race", "name");
                DependencyIds = Strings(row, "Dependency", "cardId");
                if (Classes.Count == 0) throw new InvalidDataException("Missing export cardClasses: " + CardId);
                var tags = new Dictionary<int, int>();
                foreach (var tag in row.Elements("Tag"))
                {
                    int id = Number(tag, "id", 1);
                    if (tags.ContainsKey(id)) throw new InvalidDataException("Duplicate catalog tag: " + CardId);
                    tags.Add(id, Number(tag, "value", int.MinValue));
                }
                Tags = new ReadOnlyDictionary<int, int>(tags);
            }
        }

        private static string Required(XElement row, string name)
        {
            string value = (string)row.Attribute(name);
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("Missing catalog " + name);
            return value;
        }
        private static int Number(XElement row, string name, int minimum)
        {
            if (!int.TryParse(Required(row, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < minimum)
                throw new InvalidDataException("Invalid catalog " + name);
            return value;
        }
        private static IReadOnlyList<string> Strings(XElement row, string element, string attribute)
        {
            var values = row.Elements(element).Select(e => Required(e, attribute)).ToArray();
            if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw new InvalidDataException("Duplicate catalog " + element);
            return Array.AsReadOnly(values);
        }

        internal static OfflinePoolPolicy Parse(XDocument document)
        {
            var root = document.Root;
            if (root == null || root.Name != "OfflinePoolCatalog") throw new InvalidDataException("Invalid offline pool catalog");
            string version = Required(root, "policyVersion"), build = Required(root, "clientBuild");
            if ((version != "offline-standard/1" && version != "offline-standard/2") || build != "37.0.0.254477") throw new InvalidDataException("Unsupported offline pool policy/build");
            if (root.Element("Sets") == null || root.Element("Members") == null || root.Element("Dependencies") == null)
                throw new InvalidDataException("Missing catalog section");
            var sets = Strings(root.Element("Sets"), "Set", "name").ToArray();
            string activeMeta = null;
            var metas = Array.Empty<Meta>();
            if (version == "offline-standard/1")
            {
                // Frozen golden catalogs retain their original seven-set policy and hash.
                if (!sets.SequenceEqual(StandardSets)) throw new InvalidDataException("Offline Standard set order mismatch");
            }
            else
            {
                activeMeta = Required(root, "activeMeta");
                metas = (root.Element("Metas")?.Elements("Meta") ?? Enumerable.Empty<XElement>()).Select(e => new Meta(e)).ToArray();
                if (metas.Select(m => m.Id).Distinct(StringComparer.Ordinal).Count() != metas.Length ||
                    metas.Count(m => m.Id == activeMeta) != 1 || !sets.SequenceEqual(metas.Single(m => m.Id == activeMeta).Sets))
                    throw new InvalidDataException("Invalid active meta or set order");
                var identitiesBySet = metas.SelectMany(m => m.SetIds).GroupBy(p => p.Key);
                if (identitiesBySet.Any(g => g.Select(p => p.Value).Distinct().Count() != 1) ||
                    metas.SelectMany(m => m.SetIds).GroupBy(p => p.Value).Any(g => g.Select(p => p.Key).Distinct().Count() != 1))
                    throw new InvalidDataException("Conflicting meta set identity");
            }
            var knownSets = new HashSet<string>(metas.SelectMany(m => m.Sets), StringComparer.Ordinal);
            var activeExtras = metas.SingleOrDefault(m => m.Id == activeMeta)?.ExtraCards ?? Array.Empty<string>();
            var knownExtras = new HashSet<string>(metas.SelectMany(m => m.ExtraCards), StringComparer.Ordinal);
            var members = root.Element("Members").Elements("Card").Select(e => new Member(e)).ToArray();
            if (members.Length == 0 || members.Select(m => m.CardId).Distinct(StringComparer.Ordinal).Count() != members.Length ||
                members.Select(m => m.DbfId).Distinct().Count() != members.Length)
                throw new InvalidDataException("Duplicate or conflicting catalog members");
            foreach (var member in members)
            {
                if (member.Set == "INVALID" || member.Set == "UNKNOWN" ||
                    member.Scope != (sets.Contains(member.Set) || activeExtras.Contains(member.CardId) ? "Standard" :
                        knownSets.Contains(member.Set) || knownExtras.Contains(member.CardId) ? "Inactive" : "Past"))
                    throw new InvalidDataException("Unresolved or conflicting catalog set/scope: " + member.CardId);
                if (!new[] { "standard-module", "effect-module", "core-reprint", "core-rebalanced", "legacy" }.Contains(member.RegistrationKind))
                    throw new InvalidDataException("Unknown registration kind: " + member.CardId);
            }
            var dependencies = root.Element("Dependencies").Elements("Card").Select(e => new Dependency(e)).ToArray();
            if (dependencies.Select(d => d.CardId).Distinct(StringComparer.Ordinal).Count() != dependencies.Length ||
                dependencies.Select(d => d.DbfId).Distinct().Count() != dependencies.Length)
                throw new InvalidDataException("Duplicate catalog dependency identity");
            var identities = members.ToDictionary(m => m.CardId, m => m.DbfId, StringComparer.Ordinal);
            foreach (var dependency in dependencies)
            {
                if ((identities.TryGetValue(dependency.CardId, out int id) && id != dependency.DbfId) ||
                    identities.Any(pair => pair.Value == dependency.DbfId && pair.Key != dependency.CardId))
                    throw new InvalidDataException("Conflicting catalog dependency identity");
                identities[dependency.CardId] = dependency.DbfId;
            }
            foreach (string id in members.SelectMany(m => m.DependencyIds).Concat(dependencies.SelectMany(d => d.DependencyIds)))
                if (!identities.ContainsKey(id)) throw new InvalidDataException("Missing catalog dependency: " + id);
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var source in root.Elements("Source"))
            {
                string name = Required(source, "name"), hash = Required(source, "sha256");
                if (hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c)) || hashes.ContainsKey(name))
                    throw new InvalidDataException("Invalid catalog provenance");
                hashes.Add(name, hash);
            }
            if (hashes.Count != 2 || !hashes.ContainsKey("cards.json.gz") || !hashes.ContainsKey("set-overrides.json"))
                throw new InvalidDataException("Missing catalog provenance");
            return new OfflinePoolPolicy(version, build, sets, members, dependencies, hashes, activeMeta, metas);
        }

        /// <summary>Explicit post-Cards-load check. Never called by the Cards static initializer.</summary>
        public void ValidateRegistration()
        {
            var cards = Cards.All.ToDictionary(c => c.Id, StringComparer.Ordinal);
            var standard = new HashSet<string>(CardSets.Client.StandardSets.CollectibleIds, StringComparer.Ordinal);
            bool Registered(Card card) => EffectModules.ByDbfId.ContainsKey(card.AssetId) || Reprints.ByDbfId.ContainsKey(card.AssetId)
                || RebalancedCore2026.ByDbfId.ContainsKey(card.AssetId) || CardDefs.Instance.Get.ContainsKey(card.Id);
            foreach (var member in Members)
            {
                if (!cards.TryGetValue(member.CardId, out var card) || card.AssetId != member.DbfId || !card.Collectible || !card.Implemented || !Registered(card))
                    throw new InvalidDataException("Unregistered/mismatched pool member: " + member.CardId);
                bool kind = member.RegistrationKind == "standard-module" ? standard.Contains(card.Id) && EffectModules.ByDbfId.ContainsKey(card.AssetId) :
                    member.RegistrationKind == "effect-module" ? EffectModules.ByDbfId.ContainsKey(card.AssetId) :
                    member.RegistrationKind == "core-reprint" ? Reprints.ByDbfId.ContainsKey(card.AssetId) :
                    member.RegistrationKind == "core-rebalanced" ? RebalancedCore2026.ByDbfId.ContainsKey(card.AssetId) : CardDefs.Instance.Get.ContainsKey(card.Id);
                if (!kind) throw new InvalidDataException("Pool registration kind mismatch: " + member.CardId);
            }
            foreach (var dependency in Dependencies)
                if (!cards.TryGetValue(dependency.CardId, out var card) || card.AssetId != dependency.DbfId || !Registered(card))
                    throw new InvalidDataException("Unregistered/mismatched pool dependency: " + dependency.CardId);
        }
    }
}
