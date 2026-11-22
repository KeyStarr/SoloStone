using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class LocalProfile
{
    // Deck-code import: a new personal deck with its cards, sideboards and name in ONE commit. It never grants a card
    // (unowned copies stay as missing slots) and never leaves an empty draft behind: a refused or failed commit changes nothing.
    public LocalCustomDeck CreateDraftFromCode(string name, string hero, IEnumerable<string> ids, IEnumerable<LocalSideboard> sideboards)
    {
        var boards = (sideboards ?? Enumerable.Empty<LocalSideboard>()).Select(Copy).ToList();
        LocalDeck draft = LedgerDeck(name, hero, ids, boards, false);
        if (!LocalCardCatalog.WithinCopyLimits(draft.CardIds))
            throw new InvalidOperationException("Deck exceeds the playable copy limit across printings.");
        if (data.CustomDecks.Count >= MaximumCustomDecks) throw new InvalidOperationException(PersonalDeckLimitMessage);
        EnsureAllocatorAvailable(data.NextCustomDeckId);
        var next = Snapshot();
        long id = next.NextCustomDeckId;
        next.NextCustomDeckId = checked(id + 1);
        var created = new LocalCustomDeck { Id = id, SortOrder = id, Name = draft.Name, Hero = draft.Hero,
            CardEntries = draft.CardIds.Select(card => new LocalDeckCard { Id = card }).ToList(), Sideboards = boards };
        LocalCrafting.AssignOwnedPremiums(created, DeckOwnedCount(next, profileOptions.AllCosmetics), true); // best owned versions; no grants
        next.CustomDecks.Add(created);
        Commit(next);
        return GetDraft(id);
    }
}
