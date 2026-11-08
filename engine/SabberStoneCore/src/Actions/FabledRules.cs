using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;

namespace SabberStoneCore.Actions
{
    /// <summary>Fixed constructed bundles and original companion storage; all live state uses existing payload tags.</summary>
    public static class FabledRules
    {
        private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Bundles =
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) {
                ["TIME_009"] = Array.AsReadOnly(new[] { "TIME_009t1", "TIME_009t2" }),
                ["TIME_020"] = Array.AsReadOnly(new[] { "TIME_020t1", "TIME_020t2" }),
                ["TIME_209"] = Array.AsReadOnly(new[] { "TIME_209t", "TIME_209t2" }),
                ["TIME_211"] = Array.AsReadOnly(new[] { "TIME_211t1", "TIME_211t2" }),
                ["TIME_609"] = Array.AsReadOnly(new[] { "TIME_609t1", "TIME_609t2" }),
                ["TIME_619"] = Array.AsReadOnly(new[] { "TIME_619t", "TIME_619t2" }),
                ["TIME_850"] = Array.AsReadOnly(new[] { "TIME_850t", "TIME_850t1" }),
                ["TIME_852"] = Array.AsReadOnly(new[] { "TIME_852t1", "TIME_852t3" }),
                ["TIME_875"] = Array.AsReadOnly(new[] { "TIME_875t", "TIME_875t1" }),
                ["TIME_890"] = Array.AsReadOnly(new[] { "TIME_890t", "TIME_890t2" }),
                ["TIME_005"] = Array.AsReadOnly(new[] { "TIME_005t1", "TIME_005t2", "TIME_005t3", "TIME_005t4", "TIME_005t5", "TIME_005t6", "TIME_005t7", "TIME_005t8", "TIME_005t9" })
            };
        public static IReadOnlyList<string> Bundle(string cardId) =>
            cardId != null && Bundles.TryGetValue(cardId, out var bundle) ? bundle : Array.Empty<string>();
        public static bool IsCompanion(string cardId) => Bundles.Values.Any(bundle => bundle.Contains(cardId));
        public static bool IsFabled(string cardId) => cardId != null && Bundles.ContainsKey(cardId);

        public static IEnumerable<string> DeckErrors(IEnumerable<string> ids)
        {
            var counts = ids.GroupBy(id => id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            if (counts.TryGetValue("TIME_005", out int rafaams) && rafaams != 1)
                yield return "Fabled+ requires exactly one Timethief Rafaam.";
            if (counts.ContainsKey("TIME_005") && counts.Values.Sum() > 40)
                yield return "Fabled+ permits at most 40 cards including companions.";
            var sizes = counts.Keys.Select(DeckSizeCard).Where(card => card != null && card[(GameTag)997] > 0)
                .GroupBy(card => card[(GameTag)997]).ToArray();
            if (sizes.Length > 1)
                yield return "Deck-size cards with different limits cannot share a deck: "
                    + string.Join(", ", sizes.Select(group => group.First().Name + " (" + group.Key + " cards)")) + ".";
            foreach (var row in Bundles)
            {
                counts.TryGetValue(row.Key, out int copies);
                foreach (string companion in row.Value)
                {
                    counts.TryGetValue(companion, out int companions);
                    if (companions != copies)
                        yield return "Fabled bundle " + row.Key + " requires exactly one " + companion + " per copy.";
                }
            }
        }

        private static Card DeckSizeCard(string id)
        {
            // Unknown saved definitions are validated by the caller, not by deck-size selection.
            try { return Cards.FromId(id); }
            catch (KeyNotFoundException) { return null; }
        }

        public static int DeckSize(IEnumerable<string> ids)
        {
            int size = ids.Select(DeckSizeCard).Where(card => card != null)
                .Select(card => card[(GameTag)997]).FirstOrDefault(value => value > 0);
            return size > 0 ? size : 30;
        }

        // Retain physical identity once, without subscribing offboard powers or copying the reward.
        public static void Vanish(IPlayable companion)
        {
            companion.Zone.Remove(companion);
            PayloadRules.Retain(companion.Controller, companion.Card.AssetId, companion);
            companion.Controller.SetasideZone.Add(companion);
        }

        public static IPlayable ReturnVanished(Controller recipient, int dbfId)
        {
            var original = new[] { recipient, recipient.Opponent }.SelectMany(c => PayloadRules.Entries(c, dbfId))
                .OrderBy(c => c.Id).FirstOrDefault();
            if (original == null) return null;
            original.Zone.Remove(original);
            PayloadRules.Remove(original);
            original[PayloadRules.Stored] = 0;
            original.Controller = recipient;
            original[GameTag.CONTROLLER] = recipient.PlayerId;
            // As with backlog delivery, SHOW must carry the private destination, not SETASIDE.
            original[GameTag.ZONE] = (int)Zone.HAND;
            Generic.AddHandPhase(recipient, original);
            return original;
        }

        public static IEnumerable<string> ExpandForDeckbuilding(IEnumerable<string> ids)
        {
            foreach (string id in ids)
            {
                yield return id;
                foreach (string companion in Bundle(id)) yield return companion;
            }
        }
    }
}
