using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client
{
	/// <summary>Client reprints registered by dbfId; powers are shared with the unchanged legacy module.</summary>
	public static class Reprints
	{
		private static readonly Dictionary<int, int> Aliases = new Dictionary<int, int>();
		private static readonly IReadOnlyDictionary<int, int> Registry = new ReadOnlyDictionary<int, int>(Aliases);

		/// <summary>Client dbfId to original module dbfId.</summary>
		public static IReadOnlyDictionary<int, int> ByDbfId
		{
			get { _ = Cards.Count; return Registry; }
		}

		internal static void Apply(Dictionary<string, Card> cards)
		{
			foreach (var definition in ClientOverlay.Data.Cards)
			{
				if (definition.OriginalId == null) continue;
				if (!cards.TryGetValue(definition.OriginalId, out Card original) ||
					original.AssetId != definition.OriginalDbfId ||
					definition.CardId == definition.OriginalId)
					throw new InvalidDataException("Invalid reprint module: " + definition.CardId);
				Card reprint = cards[definition.CardId];
				reprint.UseBehaviourFrom(original);
				Aliases.Add(reprint.AssetId, original.AssetId);
			}
		}
	}
}
