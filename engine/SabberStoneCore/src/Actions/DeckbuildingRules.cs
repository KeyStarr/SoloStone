using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    /// <summary>Submitted selections become controller payloads before any game action.</summary>
    public static class DeckbuildingRules
    {
        public const string Beatrix = "JAIL_397", Underbelly = "JAIL_831";
        public static bool HasSideboard(string source) => source == Beatrix || source == Underbelly;
        public static int SlotCount(string source) => source == Beatrix ? 1 : source == Underbelly ? 3 : 0;
        public static bool Eligible(string source, Card card, FormatType format)
        {
            if (card == null || !card.Collectible || !card.Implemented || card.Type != CardType.MINION) return false;
            if (format != FormatType.FT_WILD && (format != FormatType.FT_STANDARD || !StandardSets.CollectibleIds.Contains(card.Id))) return false;
            if (source == Beatrix) return card.Cost == 2 && CardClassRules.IsAllowedInDeck(card, CardClass.PALADIN);
            int mask = card[GameTag.MULTIPLE_CLASSES];
            return source == Underbelly && (card.Races.Contains(Race.BEAST) || card.Races.Contains(Race.ALL))
                && card.Class != CardClass.HUNTER && card.Class != CardClass.NEUTRAL && card.Class != CardClass.INVALID
                && card.MultiClassGroup == 0 && (mask == 0 || (mask & (mask - 1)) == 0);
        }
        public static void Validate(IEnumerable<string> main, IReadOnlyDictionary<string, string[]> sideboards,
            FormatType format, bool complete = true)
        {
            var ids = main.ToArray();
            foreach (var row in sideboards ?? new Dictionary<string, string[]>())
            {
                if (!HasSideboard(row.Key) || !ids.Contains(row.Key) || row.Value == null || row.Value.Length > SlotCount(row.Key)
                    || row.Value.Any(id => id == null || !Eligible(row.Key, Cards.FromId(id), format)))
                    throw new InvalidOperationException("Invalid deckbuilding sideboard.");
                if (row.Value.GroupBy(id => id).Any(g => g.Count() > (Cards.FromId(g.Key).Rarity == Rarity.LEGENDARY ? 1 : 2)))
                    throw new InvalidOperationException("Sideboard exceeds copy limit.");
                if (row.Key == Beatrix && row.Value.Any(ids.Contains))
                    throw new InvalidOperationException("Beatrix selection cannot also appear in the main deck.");
            }
            if (complete)
                foreach (string source in ids.Where(HasSideboard).Distinct())
                    if (sideboards == null || !sideboards.TryGetValue(source, out var slots) || slots.Length != SlotCount(source))
                        throw new InvalidOperationException("Missing sideboard selections for " + source);
        }
        internal static void Bind(Controller owner, IReadOnlyDictionary<string, string[]> sideboards, FormatType format)
        {
            Validate(owner.DeckCards.Select(c => c.Id), sideboards, format);
            if (sideboards == null) return;
            foreach (var row in sideboards.OrderBy(row => row.Key, StringComparer.Ordinal))
                foreach (string id in row.Value)
                    PayloadRules.StoreCard(owner, Cards.FromId(row.Key).AssetId, Cards.FromId(id), 0);
        }
        public static IPlayable[] Remaining(Controller owner, string source) => PayloadRules.Entries(owner, Cards.FromId(source).AssetId);
        internal static void Prepare(Game game)
        {
            foreach (var owner in new[] { game.Player1, game.Player2 })
            {
                var selected = Remaining(owner, Beatrix).SingleOrDefault();
                if (selected == null || selected[PayloadRules.Value] != 0) continue;
                selected[PayloadRules.Value] = 1;
                var creator = owner.DeckZone.First(p => p.Card.Id == Beatrix);
                for (int i = 0; i < 10; i++)
                    Entity.FromCard(owner, selected.Card, new EntityData {
                        [GameTag.CREATOR] = creator.Id, [GameTag.DISPLAYED_CREATOR] = creator.Id
                    }, owner.DeckZone);
            }
        }
        public static void Receive(Controller owner, IPlayable offer)
        {
            if (offer == null) return;
            var slot = Remaining(owner, Underbelly).FirstOrDefault(p => p.Card.Id == offer.Card.Id);
            if (slot == null) return;
            PayloadRules.Remove(slot);
            // A fresh result retains its additive discount through normal hand-entry and overflow policy.
            var result = Entity.FromCard(owner, offer.Card, new EntityData { [GameTag.ZONE] = (int)Zone.HAND,
                [GameTag.CREATOR] = offer[GameTag.CREATOR], [GameTag.DISPLAYED_CREATOR] = offer[GameTag.DISPLAYED_CREATOR] });
            SabberStoneCore.Enchants.Effects.ReduceCost(3).ApplyTo(result);
            result[GameTag.ZONE] = (int)Zone.HAND;
            if (!DrawReplacementRules.RetainOverdraw(owner, result)) Generic.AddHandPhase(owner, result);
        }
    }
}
