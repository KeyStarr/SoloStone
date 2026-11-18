#region copyright
// SabberStone, Hearthstone Simulator in C# .NET Core
// Copyright (C) 2017-2019 SabberStone Team, darkfriend77 & rnilva
//
// SabberStone is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License.
// SabberStone is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
#endregion
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.Loader
{
	internal interface ICardLoader
	{
		Card[] Load();
	}

	internal class CardLoader : ICardLoader
	{
		public Card[] Load()
		{
			// Get definitions from the embedded per-meta card file
			var cards = CardFacts.Embedded.Cards.Select(e => Build(e, overlay: false)).ToArray();
            return ClientOverlay.AddTo(ApplyIdentity(cards, CardFacts.Embedded.Identity), ClientOverlay.Data);
        }

		// Wild identity rows (facts/README.md): each replaces the base card with the same id and dbf, in its place, so the
		// card list order (random pools, first card per dbf) stays. Built with the overlay rules, as client-export data.
		internal static Card[] ApplyIdentity(Card[] baseCards, CardFacts.Entity[] identity)
		{
			if (identity == null || identity.Length == 0) return baseCards;
			var index = new Dictionary<string, int>(baseCards.Length, StringComparer.Ordinal);
			for (int i = 0; i < baseCards.Length; i++)
				if (!index.ContainsKey(baseCards[i].Id)) index.Add(baseCards[i].Id, i);
				else index[baseCards[i].Id] = -1;
			var result = (Card[])baseCards.Clone();
			var done = new HashSet<string>(StringComparer.Ordinal);
			foreach (CardFacts.Entity entity in identity)
			{
				if (entity?.Id == null || !done.Add(entity.Id))
					throw new InvalidDataException("Wild identity row duplicate or without id: " + entity?.Id);
				if (!index.TryGetValue(entity.Id, out int at) || at < 0)
					throw new InvalidDataException("Wild identity row matches no single base card: " + entity.Id);
				if (baseCards[at].AssetId != entity.AssetId)
					throw new InvalidDataException("Wild identity row changes the dbf of " + entity.Id);
				if (!new[] { GameTag.CARDTYPE, GameTag.CARD_SET, GameTag.COLLECTIBLE }.All(tag => entity.Tags.Any(t => t.GameTag == tag)))
					throw new InvalidDataException("Wild identity row missing type/set/collectible: " + entity.Id);
				if (TagOf(entity, GameTag.CARDTYPE) != (int)baseCards[at].Type)
					throw new InvalidDataException("Wild identity row changes the card type of " + entity.Id);
				if (TagOf(entity, GameTag.COLLECTIBLE) != 1)
					throw new InvalidDataException("Wild identity row is not collectible: " + entity.Id);
				result[at] = Build(entity, overlay: true);
			}
			return result;
		}

		private static int TagOf(CardFacts.Entity entity, GameTag tag)
		{
			foreach (var t in entity.Tags) if (t.GameTag == tag) return (int)t.TagValue;
			return int.MinValue;
		}

		// Private tools and tests still build cards from the XML; the engine itself does not.
        internal static Card[] LoadDefinitions(XDocument cardDefsXml, bool overlay = false)
        {
			return cardDefsXml.Descendants("Entity").Select(e => Build(ParseEntity(e), overlay)).ToArray();
		}

		internal static CardFacts.Entity ParseEntity(XElement r)
		{
			return new CardFacts.Entity
			{
				Id = r.Attribute("CardID").Value,
				AssetId = Int32.Parse(r.Attribute("ID").Value),

				// Unfortunately the file contains some duplicate tags
				// so we have to make a list first and weed out the unique ones
				Tags = (from tag in r.Descendants("Tag")
						select new Tag(
							gameTag: (GameTag)Enum.Parse(typeof(GameTag), tag.Attribute("enumID").Value),
							tagValue:
							tag.Attribute("value") != null
								? (TagValue)Int32.Parse(tag.Attribute("value").Value)
								: (tag.Attribute("type").Value == "String"
									? (TagValue)tag.Value
									: (tag.Attribute("type").Value == "LocString"
										? (TagValue)tag.Element("enUS").Value
										: (TagValue)0
									)))).ToArray(),
				Requirements = (from req in r.Descendants("PlayRequirement")
								select new
								{
									Req = (PlayReq)Enum.Parse(typeof(PlayReq), req.Attribute("reqID").Value),
									Param = (req.Attribute("param").Value != "" ? Int32.Parse(req.Attribute("param").Value) : 0)
								}).Distinct() // avoiding duplicate playrequirment unsure if it is intended or not ...
								.ToDictionary(x => x.Req, x => x.Param),
				Entourage = (from ent in r.Descendants("EntourageCard")
							 select ent.Attribute("cardID").Value).ToArray(),
				ReferencedTags = (from rtag in r.Descendants("ReferencedTag")
								  select new Tag(
									  gameTag: (GameTag)Enum.Parse(typeof(GameTag), rtag.Attribute("enumID").Value),
									  tagValue:
									  rtag.Attribute("value") != null
										  ? (TagValue)Int32.Parse(rtag.Attribute("value").Value)
										  : (rtag.Attribute("type").Value == "String"
											  ? (TagValue)rtag.Value
											  : (rtag.Attribute("type").Value == "LocString"
												  ? (TagValue)rtag.Element("enUS").Value
												  : (TagValue)0
											  )))).ToArray()
			};
		}

		// The card file and the XML meet here, so both get the same overlay rules. An entity can build
		// more than once (scoped snapshots), so each Card gets its own requirement dictionary.
		internal static Card Build(CardFacts.Entity entity, bool overlay)
		{
			// Skip PlaceholderCard etc.
			//if (!dbfCards.ContainsKey(card.Id))
			//    continue;
			var tags = entity.Tags.ToList();
            if (overlay && tags.Any(t => t.GameTag == GameTag.CARDTYPE && (int)t.TagValue == (int)CardType.WEAPON)
                && !tags.Any(t => t.GameTag == GameTag.DURABILITY))
                tags.Add(new Tag(GameTag.DURABILITY, (TagValue)(int)tags.FirstOrDefault(t => t.GameTag == GameTag.HEALTH).TagValue));
            // Legacy keyword preprocessing treats presence as true even for zero-valued tags.
            var zeros = overlay ? tags.Where(t => t.TagValue.HasIntValue && (int)t.TagValue == 0).ToArray() : Array.Empty<Tag>();
            var card = new Card(entity.Id, entity.AssetId, tags.Where(t => !zeros.Contains(t)).ToArray(),
                    entity.Requirements == null ? new Dictionary<PlayReq, int>() : new Dictionary<PlayReq, int>(entity.Requirements),
                    entity.Entourage == null ? Array.Empty<string>() : (string[])entity.Entourage.Clone(),
                    entity.ReferencedTags ?? Array.Empty<Tag>(), preserveText: overlay);
            foreach (var tag in zeros) card.Tags.Add(tag.GameTag, 0);
			return card;
		}
	}
}
