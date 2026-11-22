using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SabberStoneCore.Model;
using EngineCard = SabberStoneCore.Model.Card;

// Deck-code import, client-free: turns the card ids a pasted code holds into the deck SoloStone can save. Import grants
// nothing: ownership is never consulted here, unowned copies simply stay in the draft as missing slots.
internal enum DeckCodeRefusalReason { UnsupportedClass, Empty, NoUsableCards, Oversize, Malformed }

internal sealed class DeckCodeRefusal : Exception
{
    internal readonly DeckCodeRefusalReason Reason;
    internal DeckCodeRefusal(DeckCodeRefusalReason reason, string message) : base(message) { Reason = reason; }
}

internal sealed class DeckCodePlan
{
    internal string Hero, Name;
    internal List<string> Cards = new List<string>();
    internal List<LocalSideboard> Sideboards = new List<LocalSideboard>();
    // Copies the code held (main deck plus sideboards) and what became of the ones that were not kept.
    internal int CodeCards, Unusable, WrongClass, OverLimit, SideboardCardsDropped;
    internal int Dropped => Unusable + WrongClass + OverLimit + SideboardCardsDropped;
}

internal static class NativeDeckCodeImport
{
    // Wild-only cards are not imported today: the usable set follows the profile ledger, which PR #136 decides (owner ruling 3).

    // 32 UTF-16 code units, the limit LocalShop.ValidateDeck puts on a saved deck name (not 32 visible characters).
    internal const int NameLength = 32;
    private const int MaximumCodeCopies = 200; // a real code holds at most 40 main cards plus a few sideboard cards

    // The editor's universe: the legacy custom pool, or a Standard card the profile ledger knows. Only collectible cards: tokens and
    // other uncollectible cards never enter through a hand-made code (Fabled companions ride with their primary). CATA_190h
    // (Deathwing, Worldbreaker) is a collectible hero card in the engine data, the ledger and the editor, so it is usable too.
    internal static bool Usable(string id) => id != null && Known(id) && Cards.FromId(id).Collectible
        && (LocalDecks.IsCustomCard(id) || LocalProfile.KnownLedgerCard(id) && LocalShop.IsStandardCard(Cards.FromId(id)));

    private static bool Known(string id)
    {
        try { return Cards.FromId(id) != null; }
        catch (KeyNotFoundException) { return false; }
    }

    // The client fills a just-imported deck through the edit-mode paste: skipped (silently) only for that deck, once. The skip is
    // dropped when it is used, when the Collection is left, and when the next import starts.
    internal sealed class NativeFillSkip
    {
        private long deck;
        internal void Arm(long importedDeck) { deck = importedDeck; }
        internal void Reset() { deck = 0; }
        internal bool TryConsume(bool inCollection, long editedDeck)
        {
            long armed = deck;
            if (!inCollection) { deck = 0; return false; }
            if (armed == 0 || editedDeck != armed) return false;
            deck = 0;
            return true;
        }
    }

    internal static DeckCodePlan Plan(string hero, string codeName, string fallbackName,
        IEnumerable<(string id, int qty)> main, IEnumerable<(string owner, string id, int qty)> side)
    {
        if (hero == null || !LocalDecks.PlayerHeroes.Contains(hero))
            throw new DeckCodeRefusal(DeckCodeRefusalReason.UnsupportedClass, "This deck code is for a class SoloStone does not have.");
        var mainRows = (main ?? Enumerable.Empty<(string, int)>()).ToList();
        var sideRows = (side ?? Enumerable.Empty<(string, string, int)>()).ToList();
        long total = mainRows.Sum(row => (long)row.qty) + sideRows.Sum(row => (long)row.qty);
        if (mainRows.Any(row => row.qty < 0) || sideRows.Any(row => row.qty < 0) || total > MaximumCodeCopies)
            throw new DeckCodeRefusal(DeckCodeRefusalReason.Malformed, "This deck code is damaged.");
        if (mainRows.Sum(row => row.qty) == 0)
            throw new DeckCodeRefusal(DeckCodeRefusalReason.Empty, "This deck code holds no cards.");
        var plan = new DeckCodePlan { Hero = hero, CodeCards = (int)total, Name = Name(hero, codeName, fallbackName) };
        var heroClass = LocalDecks.HeroClass(hero);

        // 1. Usable, class-legal cards in code order. Fabled companions are never listed: each primary brings its own.
        var legal = new List<string>();
        foreach (var row in mainRows)
            for (int copy = 0; copy < row.qty; copy++)
            {
                // A companion rides with its primary; one listed without its primary is dropped and counted.
                if (row.id != null && LocalDecks.IsFabledCompanion(row.id))
                {
                    if (!mainRows.Any(other => other.id != null && other.qty > 0 && LocalDecks.FabledBundle(other.id).Contains(row.id))) plan.Unusable++;
                    continue;
                }
                if (!Usable(row.id) || LocalDecks.IsUnsupportedSideboardOwner(row.id)) { plan.Unusable++; continue; }
                if (!LocalDecks.IsAllowedInDeck(Cards.FromId(row.id), heroClass)) { plan.WrongClass++; continue; }
                legal.Add(row.id);
            }

        // 2. Trim to the playable copy limits (two, one for a Legendary, across printings of one card) and the rune limit.
        var kept = new List<string>();
        var perGroup = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string id in legal)
        {
            string group = LocalCardCatalog.CopyGroup(id);
            perGroup.TryGetValue(group, out int copies);
            if (copies + 1 > CopyLimit(id) || !RunesFit(kept, id, heroClass)) { plan.OverLimit++; continue; }
            perGroup[group] = copies + 1;
            kept.Add(id);
        }

        // 3. Cards that carry a deck-size rule cannot share a deck with a different one: the first stays.
        int? size = null;
        for (int index = 0; index < kept.Count; index++)
        {
            int own = LocalDecks.DeckSize(new[] { kept[index] });
            if (own == 30) continue;
            if (size == null) size = own;
            else if (own != size) { kept.RemoveAt(index--); plan.OverLimit++; }
        }
        var ids = new List<string>();
        foreach (string id in kept) { ids.Add(id); ids.AddRange(LocalDecks.FabledBundle(id)); }
        if (ids.Count == 0)
            throw new DeckCodeRefusal(DeckCodeRefusalReason.NoUsableCards, "None of the cards in this deck code can be used here.");
        if (ids.Count > LocalDecks.DraftLimit(ids))
            throw new DeckCodeRefusal(DeckCodeRefusalReason.Oversize,
                "This deck code holds more cards than a deck can: " + ids.Count + " of " + LocalDecks.DraftLimit(ids) + ".");
        try { LocalDecks.ValidateFabled(ids); }
        catch (InvalidOperationException error) { throw new DeckCodeRefusal(DeckCodeRefusalReason.Malformed, error.Message); }
        plan.Cards = ids;

        // 4. Sideboards: an owner SoloStone models, in the deck, with eligible cards that fit; anything else is dropped whole.
        foreach (var owner in sideRows.GroupBy(row => row.owner ?? "", StringComparer.Ordinal))
        {
            var wanted = owner.SelectMany(row => Enumerable.Repeat(row.id, row.qty)).ToList();
            var board = new List<string>();
            if (owner.Key.Length > 0 && ids.Contains(owner.Key) && LocalDecks.HasModelledSideboard(owner.Key))
                foreach (string id in wanted)
                    if (id != null && LocalDecks.SideboardEligible(owner.Key, id) && board.Count < LocalDecks.SideboardSlotCount(owner.Key)) board.Add(id);
            bool accepted = board.Count > 0 && FitsSideboard(ids, owner.Key, board);
            if (accepted) plan.Sideboards.Add(new LocalSideboard { Source = owner.Key, Cards = board });
            plan.SideboardCardsDropped += wanted.Count - (accepted ? board.Count : 0);
        }
        return plan;
    }

    private static int CopyLimit(string id) => LocalCoreProgression.ById.TryGetValue(id, out var core) ? core.Copies
        : Cards.FromId(id).Rarity == SabberStoneCore.Enums.Rarity.LEGENDARY ? 1 : 2;

    private static bool RunesFit(List<string> kept, string id, SabberStoneCore.Enums.CardClass heroClass)
    {
        try { LocalDecks.ValidateRunes(kept.Concat(new[] { id }).Select(Cards.FromId), heroClass); return true; }
        catch (InvalidOperationException) { return false; }
    }

    private static bool FitsSideboard(IEnumerable<string> main, string owner, List<string> board)
    {
        try { LocalDecks.ValidateNativeSave(main, new[] { new LocalSideboard { Source = owner, Cards = board } }); return true; }
        catch (InvalidOperationException) { return false; }
    }

    // The deck name from the code, cut to what a deck name can hold; the usual new-deck name when it has none or none is valid.
    internal static string Name(string hero, string codeName, string fallbackName)
    {
        string name = codeName == null ? null : new string(codeName.Where(ch => !char.IsControl(ch)).ToArray()).Trim();
        if (!string.IsNullOrEmpty(name))
        {
            var elements = StringInfo.GetTextElementEnumerator(name);
            var cut = new System.Text.StringBuilder();
            while (elements.MoveNext() && cut.Length + elements.GetTextElement().Length <= NameLength) cut.Append(elements.GetTextElement());
            name = cut.ToString().Trim();
            if (ValidName(hero, name)) return name;
        }
        return fallbackName;
    }

    private static bool ValidName(string hero, string name)
    {
        try { LocalDecks.Draft(name, hero, Array.Empty<string>()); return true; }
        catch (ArgumentException) { return false; }
    }

    // What the player is told after an import; null when nothing was left out and nothing is missing.
    internal static string StatusMessage(DeckCodePlan plan, int missing)
    {
        var parts = new List<string>();
        if (plan.Unusable > 0) parts.Add(CardCount(plan.Unusable) + " that cannot be used here");
        if (plan.WrongClass > 0) parts.Add(CardCount(plan.WrongClass) + " from another class");
        if (plan.OverLimit > 0) parts.Add(CardCount(plan.OverLimit) + " over a deck limit");
        if (plan.SideboardCardsDropped > 0) parts.Add(CardCount(plan.SideboardCardsDropped) + " from a sideboard");
        string text = parts.Count == 0 ? "" : "Deck imported. Left out: " + string.Join(", ", parts) + ".";
        if (missing > 0) text += (text.Length == 0 ? "Deck imported. " : " ") + (missing == 1 ? "1 card is" : missing + " cards are") + " not in your collection yet.";
        return text.Length == 0 ? null : text;
    }

    private static string CardCount(int count) => count == 1 ? "1 card" : count + " cards";

    internal const string EditPasteMessage = "Pasting a deck code into an open deck is not available. Copy the code again, close the deck, then use Paste on the deck list.";
}
