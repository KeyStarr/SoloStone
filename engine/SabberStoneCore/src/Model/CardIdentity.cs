using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.Identity
{
	// Lives outside the namespaces the snapshot schema scans (SnapshotSchema.Namespaces): a type added in SabberStoneCore.Model would renumber
	// every published definition type id after it and change all snapshot bytes.
	/// <summary>
	/// One identity for a card and its reprints (#225). Core Ice Barrier (CORE_EX1_289) and classic Ice Barrier (EX1_289) are one
	/// card for every rule that compares cards: Secret uniqueness, random Secret picks, no-duplicates conditions, distinct picks,
	/// played-card histories and "other than this card" filters. Use <see cref="Card.IsSameCardAs"/> or <see cref="Card.Identity"/>
	/// (a canonical key; equal for reprints) in those rules, never <c>Card.Id</c> or <c>AssetId</c>. Entity identity, enchantment ids,
	/// creating, copying or transforming into a card and lookups by id stay exact.
	/// The retail rule is not pinned by a source (labelled Unpinned_* in the proofs).
	/// Legacy pool replays (schema-2 or unversioned records) keep their frozen draws, so the draws that consume a pool are gated on it (Discover, Choice,
	/// SampleDistinct of a context, Build-A-Beast). The ungated sites (Arcane Keysmith, OfflineCardPools sampling and candidates, deck fill, Secret and
	/// Highlander checks) are safe: no shipped mode uses legacy pools (every new match records match-random/1), and with no reprint pair in the
	/// input they draw exactly as before, which the recorded goldens confirm.
	/// Links, first match wins, followed to a root: tag 1413 DECK_RULE_COUNT_AS_COPY_OF_CARD_ID (dbf of the original) when the names match
	/// (one row, a Murgatha hero power, points it at Evolve); tag 858 only
	/// when both cards are collectible and share an English name (858 also joins boss and shrine cards to unrelated cards);
	/// the overlay reprint alias (<see cref="Reprints"/>); a rebalanced Core card and its original when the names match.
	/// </summary>
	public static class CardIdentity
	{
		internal const GameTag DeckRuleCountAsCopyOf = (GameTag)1413;
		internal const GameTag RelatedOriginal = (GameTag)858;

		/// <summary>The link a card has to its original (dbf to dbf), before roots are resolved. Filled once at card load.</summary>
		private static readonly Dictionary<int, int> Links = new Dictionary<int, int>();

		/// <summary>Dbf to root dbf for every card that links. Read by dbf, not stored on Card, so snapshot codecs never see it and a decoded definition keeps its identity.</summary>
		private static Dictionary<int, int> Roots = new Dictionary<int, int>();

		/// <summary>The identity key of a card dbf: the root of its links, or the dbf itself.</summary>
		internal static int Of(int dbf) => Roots.TryGetValue(dbf, out int root) ? root : dbf;

		/// <summary>Called once from the card container after the reprint modules are applied.</summary>
		internal static void Build(IEnumerable<Card> cards, Dictionary<string, Card> byId)
		{
			var byDbf = new Dictionary<int, Card>();
			foreach (Card c in cards) byDbf[c.AssetId] = c;
			var links = new Dictionary<int, int>();
			foreach (Card c in byDbf.Values)
			{
				int target = 0;
				int copyOf = c[DeckRuleCountAsCopyOf];
				if (copyOf != 0 && byDbf.TryGetValue(copyOf, out Card copied) && c.Name == copied.Name) target = copyOf;
				if (target == 0)
				{
					int related = c[RelatedOriginal];
					if (related != 0 && byDbf.TryGetValue(related, out Card other) && c.Collectible && other.Collectible && c.Name == other.Name)
						target = related;
				}
				if (target == 0 && Reprints.ByDbfId.TryGetValue(c.AssetId, out int original) && byDbf.ContainsKey(original))
					target = original;
				if (target == 0 && RebalancedCore2026.ByDbfId.TryGetValue(c.AssetId, out string originalId)
					&& byId.TryGetValue(originalId, out Card rebalanced) && rebalanced.Name == c.Name)
					target = rebalanced.AssetId;
				if (target != 0 && target != c.AssetId) links[c.AssetId] = target;
			}
			// Last link: collectible cards of one English name and one card type are one card, even when the facts carry no 858 or 1413
			// for the pair. Heroes are skipped (a hero skin shares its name with other skins). Checked in the proofs for every such group.
			foreach (var group in byDbf.Values.Where(c => c.Collectible && c.Type != CardType.HERO).GroupBy(c => (c.Name, c.Type)))
			{
				var groupRoots = group.Select(c => Resolve(links, c.AssetId)).Distinct().OrderBy(r => r).ToArray();
				for (int i = 1; i < groupRoots.Length; i++) links[groupRoots[i]] = groupRoots[0];
			}
			Links.Clear();
			foreach (KeyValuePair<int, int> link in links) Links[link.Key] = link.Value;
			var roots = new Dictionary<int, int>();
			foreach (int dbf in links.Keys) roots[dbf] = Resolve(links, dbf);
			Roots = roots;
		}

		private static int Resolve(Dictionary<int, int> links, int dbf)
		{
			// Follow to the fixed point. A cycle (a data error) resolves to its smallest dbf so the key stays stable.
			var seen = new List<int>();
			int at = dbf;
			while (links.TryGetValue(at, out int next))
			{
				if (seen.Contains(at)) return seen.GetRange(seen.IndexOf(at), seen.Count - seen.IndexOf(at)).Min();
				seen.Add(at);
				at = next;
			}
			return at;
		}

		/// <summary>Every card that links to another card, with its root dbf. For the data check.</summary>
		public static IReadOnlyDictionary<int, int> Linked
		{
			get
			{
				_ = Cards.Count;
				var result = new Dictionary<int, int>();
				foreach (int dbf in Links.Keys) result[dbf] = Resolve(Links, dbf);
				return result;
			}
		}

		/// <summary>Equality by identity: a card and its reprints are equal. For Distinct, Contains, GroupBy and sets.</summary>
		public sealed class SameCardComparer : IEqualityComparer<Card>
		{
			public bool Equals(Card a, Card b) => Same(a, b) || (a == null && b == null);
			public int GetHashCode(Card card) => card == null ? 0 : card.Identity;
		}

		/// <summary>Use with Distinct and Contains so a reprint never counts as a second card.</summary>
		public static readonly SameCardComparer Comparer = new SameCardComparer();

		/// <summary>Keeps the first printing of each card, in order. Returns the same array when nothing repeats.</summary>
		public static Card[] Distinct(Card[] cards)
		{
			var seen = new HashSet<int>();
			foreach (Card c in cards)
				if (!seen.Add(c.Identity))
				{
					seen.Clear();
					var result = new List<Card>(cards.Length);
					foreach (Card d in cards) if (seen.Add(d.Identity)) result.Add(d);
					return result.ToArray();
				}
			return cards;
		}

		/// <summary>The identity key of the card with this dbf (the dbf itself when it names no known card), for histories that keep dbfs.</summary>
		public static int OfDbf(int dbfId) => Cards.FromAssetId(dbfId)?.Identity ?? dbfId;

		/// <summary>True when both cards are one card (the same printing or a reprint of each other).</summary>
		public static bool Same(Card a, Card b) => a != null && b != null && (ReferenceEquals(a, b) || a.Identity == b.Identity);
	}
}
