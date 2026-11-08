using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.Serialization;
using System.Text;
using SabberStoneCore.Enums;

namespace SabberStoneCore.Model
{
    /// <summary>Immutable membership and metadata, independent of registration order and Game state.</summary>
    public sealed class OfflinePoolManifest
    {
        private static readonly Lazy<OfflinePoolManifest> InstalledCache =
            new Lazy<OfflinePoolManifest>(() => OfflinePoolPolicy.Load().BuildManifest());
        internal static OfflinePoolManifest Installed => InstalledCache.Value;

        public const int CurrentSchemaVersion = 1;
        public const string CurrentSamplingVersion = "partial-fisher-yates/1";
        public int SchemaVersion => CurrentSchemaVersion;
        public string PolicyVersion { get; }
        public string SamplingVersion { get; }
        public IReadOnlyList<string> Sets { get; }
        [field: NonSerialized] public int NewestSetId { get; }
        public IReadOnlyList<string> PackEligibleSets => Array.AsReadOnly(Sets.Where(s => s != "CORE").ToArray());
        public IReadOnlyList<OfflinePoolPolicy.Member> Standard { get; }
        public IReadOnlyList<OfflinePoolPolicy.Member> Past { get; }
        public string CatalogDigest { get; }
        public string Hash { get; }

        internal static Card Definition(OfflinePoolPolicy.Member member)
        {
            var card = Cards.FromId(member.CardId);
            if (member.Scope != "Past" || member.Type != "SPELL" ||
                !member.Tags.TryGetValue((int)GameTag.SPELL_SCHOOL, out int school) || card[GameTag.SPELL_SCHOOL] == school)
                return card;
            // Generated historical spells need current schools without changing frozen legacy definitions.
            var qualified = card.Clone();
            qualified.Tags[GameTag.SPELL_SCHOOL] = school;
            return qualified;
        }

        internal OfflinePoolManifest(OfflinePoolPolicy policy, string policyVersion, string samplingVersion)
        {
            PolicyVersion = policyVersion; SamplingVersion = samplingVersion;
            Sets = Array.AsReadOnly(policy.Sets.ToArray());
            var selectedMeta = policy.Metas.SingleOrDefault(m => m.Id == policy.ActiveMetaId);
            // Frozen v1 catalogs predate meta metadata and always used Black Empire.
            NewestSetId = selectedMeta == null ? 1994 : selectedMeta.SetIds[policy.NewestSet];
            var members = policy.Members.OrderBy(m => m.DbfId).ToArray();
            if (members.Select(m => m.DbfId).Distinct().Count() != members.Length ||
                members.Select(m => m.CardId).Distinct(StringComparer.Ordinal).Count() != members.Length)
                throw new InvalidDataException("Duplicate/conflicting manifest identity");
            Standard = Array.AsReadOnly(members.Where(m => m.Scope == "Standard").ToArray());
            Past = Array.AsReadOnly(members.Where(m => m.Scope == "Past").ToArray());

            var metadata = new StringBuilder("offline-pool-metadata\n");
            Record(metadata, "client-build", policy.ClientBuild);
            if (policy.ActiveMetaId != null) Record(metadata, "active-meta", policy.ActiveMetaId);
            foreach (var meta in policy.Metas.OrderBy(m => m.Id, StringComparer.Ordinal))
            {
                Record(metadata, "meta", meta.Id);
                Record(metadata, "meta-newest-set", meta.NewestSet);
                foreach (var set in meta.Sets) Record(metadata, "meta-set", set, Number(meta.SetIds[set]));
                foreach (var id in meta.ExtraCards.OrderBy(id => id, StringComparer.Ordinal))
                    Record(metadata, "meta-extra-card", id, Number(meta.ExtraCardSetIds[id]));
            }
            foreach (var pair in policy.SourceHashes.OrderBy(p => p.Key, StringComparer.Ordinal))
                Record(metadata, "source", pair.Key, pair.Value);
            foreach (var m in members)
            {
                Record(metadata, "member", Number(m.DbfId), m.CardId, m.Scope, m.Set, m.Type, Number(m.Cost), m.Rarity, m.SpellSchool);
                Record(metadata, "classes", m.Classes.OrderBy(s => s, StringComparer.Ordinal).ToArray());
                Record(metadata, "races", m.Races.OrderBy(s => s, StringComparer.Ordinal).ToArray());
                foreach (var tag in m.Tags.OrderBy(p => p.Key)) Record(metadata, "tag", Number(tag.Key), Number(tag.Value));
            }
            CatalogDigest = Sha256(metadata.ToString());
            Hash = Sha256(CanonicalText());
        }

        // UTF-8 without BOM, LF records, explicit labels, invariant numbers. Each field is
        // byte-length-prefixed (length:value), so even embedded separators cannot alias.
        internal string CanonicalText()
        {
            var text = new StringBuilder("offline-pool-manifest\n");
            Record(text, "schema", Number(SchemaVersion));
            Record(text, "policy", PolicyVersion);
            Record(text, "sampling", SamplingVersion);
            Record(text, "sets", Sets.ToArray());
            Record(text, "catalog-sha256", CatalogDigest);
            Record(text, "standard", Number(Standard.Count));
            foreach (var m in Standard) Record(text, "member", Number(m.DbfId), m.CardId);
            Record(text, "past", Number(Past.Count));
            foreach (var m in Past) Record(text, "member", Number(m.DbfId), m.CardId);
            return text.ToString();
        }

        internal static OfflinePoolManifest ValidateInstalled(string policyVersion, string manifestHash)
        {
            try
            {
                var installed = Installed;
                if (policyVersion != installed.PolicyVersion || manifestHash != installed.Hash)
                    throw new SerializationException("Snapshot offline pool policy/hash missing or mismatched.");
                return installed;
            }
            catch (InvalidDataException error)
            {
                throw new SerializationException("Invalid installed offline pool manifest.", error);
            }
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static void Record(StringBuilder text, string label, params string[] fields)
        {
            text.Append(label);
            foreach (var field in fields) text.Append(' ').Append(Number(Encoding.UTF8.GetByteCount(field))).Append(':').Append(field);
            text.Append('\n');
        }
        private static string Sha256(string text)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
