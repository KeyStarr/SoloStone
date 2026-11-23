#if !SOLOSTONE_RELEASE
using System;
using System.Collections.Generic;
using System.Linq;

// Live spike only. No profile, no persistence, no match launch.
internal sealed class ArenaStubService : IArenaService
{
    private readonly Func<string, IEnumerable<string>> pool;
    private ArenaView current = new ArenaView { EntryCost = 150, GoldBalance = 1000 };
    private string beginId;
    private int lastSlot = -1, lastIndex = -1;
    internal ArenaStubService(Func<string, IEnumerable<string>> pool) { this.pool = pool; }
    public ArenaView View() => Copy();
    public ArenaView Begin(string requestId)
    {
        if (current.Phase != ArenaPhase.None) return requestId == beginId ? Copy() : Copy("Finish the current run first.");
        if (string.IsNullOrEmpty(requestId)) return Copy("Entry request id required.");
        if (current.GoldBalance < 150) return Copy("Arena costs 150 gold. You have " + current.GoldBalance + " gold.");
        beginId = requestId;
        current = new ArenaView { Phase = ArenaPhase.ChoosingHero, EntryCost = 150, GoldBalance = current.GoldBalance - 150,
            RunId = "stub-" + requestId, HeroOffers = new List<string> { "HERO_01", "HERO_08", "HERO_06" } };
        lastSlot = lastIndex = -1;
        return Copy();
    }
    public ArenaView Choose(int slot, int index)
    {
        if (slot == lastSlot && index == lastIndex) return Copy();
        if (slot != current.Slot || index < 0 || index > 2) return Copy("Stale choice.");
        if (current.Phase != ArenaPhase.ChoosingHero && current.Phase != ArenaPhase.Drafting) return Copy("No choice pending.");
        string hero = current.Phase == ArenaPhase.ChoosingHero ? current.HeroOffers[index] : current.HeroCardId;
        string[] cards = pool(hero).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        if (cards.Length < 3) return Copy("Stub needs at least three active cards.");
        if (current.Phase == ArenaPhase.ChoosingHero)
        {
            current.HeroCardId = hero;
            current.HeroOffers.Clear();
        }
        else current.Deck.Add(current.Offers[index]);
        lastSlot = slot; lastIndex = index;
        current.Phase = current.Deck.Count == 30 ? ArenaPhase.Ready : ArenaPhase.Drafting;
        // Same numbering as ArenaService: the slot of the pending choice (hero 0, cards 1..30), and 30 once the deck is complete.
        current.Slot = current.Phase == ArenaPhase.Ready ? 30 : current.Slot + 1;
        current.Offers = current.Phase == ArenaPhase.Ready ? new List<string>()
            : Enumerable.Range(0, 3).Select(i => cards[((current.Slot - 1) * 3 + i) % cards.Length]).ToList();
        return Copy();
    }
    public ArenaView Retire()
    {
        if (current.Phase == ArenaPhase.None) return Copy("No active run.");
        if (current.Phase == ArenaPhase.RewardPending) return Copy();
        current.Phase = ArenaPhase.RewardPending;
        current.HeroOffers.Clear(); current.Offers.Clear();
        current.Reward = new ArenaReward { RunId = current.RunId, Wins = current.Wins, Gold = 100, Dust = 10,
            PackTypes = new List<string> { "STANDARD" } };
        current.GoldBalance += current.Reward.Gold;
        return Copy();
    }
    public ArenaView AcknowledgeReward(string runId)
    {
        if (current.Phase != ArenaPhase.RewardPending || runId != current.RunId) return Copy("No matching reward.");
        current = new ArenaView { EntryCost = 150, GoldBalance = current.GoldBalance };
        return Copy();
    }
    public bool StartMatch(out string error) { error = "stub"; return false; }
    private ArenaView Copy(string error = null) => new ArenaView { Phase = current.Phase, RunId = current.RunId,
        Slot = current.Slot, HeroCardId = current.HeroCardId, Wins = current.Wins, Losses = current.Losses,
        EntryCost = current.EntryCost, GoldBalance = current.GoldBalance, Error = error,
        RewardTable = Enumerable.Range(0, 13).Select(wins => new ArenaReward { Wins = wins, Gold = 75 * wins, Dust = 28 * wins,
            PackTypes = Enumerable.Repeat("STANDARD", 1 + wins / 3).ToList() }).ToList(),
        HeroOffers = new List<string>(current.HeroOffers), Offers = new List<string>(current.Offers), Deck = new List<string>(current.Deck),
        Reward = current.Reward == null ? null : new ArenaReward { RunId = current.Reward.RunId, Wins = current.Reward.Wins,
            Gold = current.Reward.Gold, Dust = current.Reward.Dust, PackTypes = new List<string>(current.Reward.PackTypes) } };
}
#endif
