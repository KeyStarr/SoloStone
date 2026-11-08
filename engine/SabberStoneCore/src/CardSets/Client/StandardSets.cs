using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client
{
    /// <summary>Collectible cards registered by the six Standard set modules.</summary>
    public static class StandardSets
    {
        private static readonly HashSet<string> Ids = new HashSet<string>(StringComparer.Ordinal);
        internal static void Record(Card card)
        {
            if (card.Collectible) Ids.Add(card.Id);
        }
        public static IReadOnlyList<string> CollectibleIds
        {
            get { _ = Cards.Count; return Array.AsReadOnly(Ids.OrderBy(id => id, StringComparer.Ordinal).ToArray()); }
        }
    }
}
