using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.Loader
{
	// Additive definitions only. Legacy XML identities are immutable.
	internal static class ClientOverlay
	{
		internal sealed class Document
		{
			public int SchemaVersion;
			public string SourceBuild;
			public string ExportSha256;
			public Definition[] Cards;
		}

		internal sealed class Definition
		{
			internal CardFacts.Entity Entity;
            public string CardId;
			public int DbfId;
			public string Name;
			public string Text;
			public Dictionary<string, int> Tags;
			public Dictionary<string, int> ReferencedTags;
			public bool ScopedSnapshot;
			public string OriginalId;
			public int OriginalDbfId;
		}

		internal static readonly Document Data = CardFacts.Embedded.Overlay;

		// The private XML, for the equality proof.
		internal static Document Read(Stream stream)
		{
            using (stream)
            {
                var root = XDocument.Load(stream).Root;
                return new Document {
                    SchemaVersion = (int)root.Attribute("schemaVersion"),
                    SourceBuild = (string)root.Attribute("sourceBuild"),
                    ExportSha256 = (string)root.Attribute("exportSha256"),
                    Cards = root.Elements("Entity").Select(e => new Definition {
                        CardId = (string)e.Attribute("CardID"), DbfId = (int)e.Attribute("ID"),
                        Name = (string)e.Elements("Tag").Single(t => (int)t.Attribute("enumID") == 185).Element("enUS"),
                        Text = (string)e.Elements("Tag").Single(t => (int)t.Attribute("enumID") == 184).Element("enUS"),
                        Tags = e.Elements("Tag").Where(t => t.Attribute("value") != null)
                            .ToDictionary(t => (string)t.Attribute("enumID"), t => (int)t.Attribute("value")),
                        ReferencedTags = e.Elements("ReferencedTag")
                            .ToDictionary(t => (string)t.Attribute("enumID"), t => (int)t.Attribute("value")),
                        ScopedSnapshot = (bool?)e.Attribute("scopedSnapshot") ?? false,
                        OriginalId = (string)e.Attribute("originalId"),
                        OriginalDbfId = (int?)e.Attribute("originalDbfId") ?? 0,
                        Entity = CardLoader.ParseEntity(e)
                    }).ToArray()
                };
            }
        }

        internal static Card Snapshot(string id)
        {
            return CreateCard(Data.Cards.Single(d => d.ScopedSnapshot && d.CardId == id));
        }

        internal static Card CreateCard(Definition definition)
        {
            // Programmatic proof definitions use the same XML parser as the private overlay.
            var entity = definition.Entity ?? CardLoader.ParseEntity(new XElement("Entity",
                new XAttribute("CardID", definition.CardId), new XAttribute("ID", definition.DbfId),
                definition.Tags.Select(t => new XElement("Tag", new XAttribute("enumID", t.Key), new XAttribute("value", t.Value))),
                new XElement("Tag", new XAttribute("enumID", 185), new XAttribute("type", "LocString"), new XElement("enUS", definition.Name)),
                new XElement("Tag", new XAttribute("enumID", 184), new XAttribute("type", "LocString"), new XElement("enUS", definition.Text)),
                (definition.ReferencedTags ?? new Dictionary<string, int>()).Select(t =>
                    new XElement("ReferencedTag", new XAttribute("enumID", t.Key), new XAttribute("value", t.Value)))));
            return CardLoader.Build(entity, overlay: true);
        }

		internal static Card[] AddTo(Card[] xmlCards, Document overlay)
		{
			if (overlay.SchemaVersion != 1 || overlay.SourceBuild != "37.0.0.254477" ||
				overlay.ExportSha256 == null || overlay.ExportSha256.Length != 64 ||
				overlay.ExportSha256.Any(c => !Uri.IsHexDigit(c)) || overlay.Cards == null)
				throw new InvalidDataException("Invalid pinned client overlay provenance");
			var ids = new HashSet<string>(xmlCards.Select(c => c.Id), StringComparer.Ordinal);
			var assets = new HashSet<int>(xmlCards.Select(c => c.AssetId));
			var result = new List<Card>(xmlCards);
            var snapshots = new HashSet<string>(StringComparer.Ordinal);
			foreach (Definition definition in overlay.Cards)
			{
                if (definition.Tags == null || !definition.Tags.ContainsKey("202") ||
                    !definition.Tags.ContainsKey("183") || !definition.Tags.ContainsKey("321"))
                    throw new InvalidDataException("Client overlay missing type/set/collectible: " + definition.CardId);
                // Current tokens can share a legacy identity but differ in base stats.
                // Keep their definitions private to the current module; never replace XML lookup.
                if (definition.ScopedSnapshot)
                {
                    if (!snapshots.Add(definition.CardId) || !xmlCards.Any(c => c.Id == definition.CardId && c.AssetId == definition.DbfId && !c.Collectible)
                        || definition.Tags["321"] != 0)
                        throw new InvalidDataException("Invalid scoped token snapshot: " + definition.CardId);
                    continue;
                }
				if (string.IsNullOrWhiteSpace(definition.CardId) || !ids.Add(definition.CardId))
					throw new InvalidDataException("Client overlay cardId collision: " + definition.CardId);
				if (definition.DbfId <= 0 || !assets.Add(definition.DbfId))
					throw new InvalidDataException("Client overlay dbfId collision: " + definition.DbfId);
                var card = CreateCard(definition);
				result.Add(card);
			}
			return result.ToArray();
		}
	}
}
