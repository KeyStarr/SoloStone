using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ArenaService : IArenaService
{
    private readonly LocalProfile profile;
    public ArenaService(LocalProfile profile) => this.profile = profile ?? throw new ArgumentNullException(nameof(profile));

    public ArenaView View()
    {
        var run = profile.ArenaRun;
        var rules = DraftRules.Arena(run?.RulesVersion ?? 1);
        return new ArenaView {
            Phase = run?.Phase ?? ArenaPhase.None, RunId = run?.RunId, Wins = run?.Wins ?? 0, Losses = run?.Losses ?? 0,
            MaxWins = rules.MaxWins, MaxLosses = rules.MaxLosses, MaxSlot = rules.DeckSize,
            Slot = run == null || run.Hero == null ? 0 : run.Picks.Count + (run.Phase == ArenaPhase.Drafting ? 1 : 0),
            HeroCardId = run?.Hero, HeroOffers = run?.HeroOffers.ToList() ?? new List<string>(),
            Offers = run?.Phase == ArenaPhase.Drafting ? run.PendingOffers.ToList() : new List<string>(),
            Deck = run?.Deck.ToList() ?? new List<string>(), Reward = ArenaRun.CopyReward(run?.Reward),
            EntryCost = rules.EntryGold, GoldBalance = profile.Gold,
            // Preview only: the generic Standard pack art. The paid set is chosen when the run ends (by missing copies), so no set is promised.
            RewardTable = rules.RewardTable(run?.GrindTier ?? profile.GrindTier.Id, DraftRules.PreviewPackType).Select(ArenaRun.CopyReward).ToList()
        };
    }

    private ArenaView Error(string error) { var view = View(); view.Error = error; return view; }
    private ArenaView Change(Action<LocalProfileData> change)
    {
        try { profile.CommitChange(next => { change(next); return true; }); return View(); }
        catch (Exception error) { return Error(error.Message); }
    }

    public ArenaView Begin(string requestId)
    {
        if (!ArenaRun.ValidRequest(requestId)) return Error("Invalid entry request id.");
        if (profile.ArenaRequestSeen(requestId)) {
            // A retry of the entry that started the current run is the same call; any other seen id is spent (its run is gone).
            var current = profile.ArenaRun;
            return current != null && current.EntryRequestId == requestId ? View() : Error("Entry request already used.");
        }
        if (profile.ArenaRun != null) return Error("An Arena run is already active.");
        if (profile.Pending != null) return Error("A match is pending.");
        if (profile.HasPendingRewardPresentation || profile.PackRevealPending) return Error("A reward presentation is pending.");
        var rules = DraftRules.Arena();
        if (profile.Gold < rules.EntryGold) return Error("Arena costs " + rules.EntryGold + " gold. You have " + profile.Gold + " gold.");
        return Change(next => {
            if (next.ArenaEntryRequestIds.Count >= LocalProfile.MaximumArenaRequests) throw new InvalidOperationException("Arena entry receipt limit reached.");
            string id = Guid.NewGuid().ToString("D");
            long seed = BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0);
            if (!profile.InfiniteGold) next.GoldBalance = checked(next.GoldBalance - rules.EntryGold);
            next.ArenaRun = new ArenaRun { RunId = id, Seed = seed, RulesVersion = rules.Version, GrindTier = profile.GrindTier.Id,
                Phase = ArenaPhase.ChoosingHero, HeroOffers = rules.HeroOffers(seed).ToList(), EntryGold = rules.EntryGold, EntryRequestId = requestId };
            next.ArenaEntryRequestIds.Add(requestId);
        });
    }

    public ArenaView Choose(int slot, int index)
    {
        var run = profile.ArenaRun;
        if (run == null) return Error("No Arena run.");
        var rules = DraftRules.Arena(run.RulesVersion);
        if (index < 0 || index >= rules.OfferSize) return Error("Invalid choice index.");
        int expected = run.Hero == null ? 0 : run.Picks.Count + 1;
        if (expected > 0 && slot == expected - 1 && (slot == 0 ? run.HeroIndex : run.Picks[slot - 1].Index) == index) return View();
        if (slot != expected || run.Phase != ArenaPhase.ChoosingHero && run.Phase != ArenaPhase.Drafting)
            return Error("Invalid choice slot.");
        return Change(next => {
            var current = next.ArenaRun;
            // The offer shown is the stored one; the rules generate an offer only when it is first created (next pick below).
            if (slot == 0) {
                current.HeroIndex = index; current.Hero = current.HeroOffers[index]; current.Phase = ArenaPhase.Drafting;
                current.PendingOffers = rules.Offers(current.Seed, 1, current.Hero, current.Deck).ToList();
            } else {
                var offers = current.PendingOffers.ToList();
                current.Picks.Add(new ArenaPick { Offers = offers, Index = index }); current.Deck.Add(offers[index]);
                if (current.Picks.Count == rules.DeckSize) { current.Phase = ArenaPhase.Ready; current.PendingOffers = new List<string>(); }
                else current.PendingOffers = rules.Offers(current.Seed, current.Picks.Count + 1, current.Hero, current.Deck).ToList();
            }
        });
    }

    public ArenaView Retire()
    {
        var run = profile.ArenaRun;
        if (run == null) return Error("No Arena run.");
        if (run.Phase == ArenaPhase.RewardPending) return View();
        if (profile.Pending != null) return Error("A match is pending.");
        return Change(next => {
            next.ArenaRun.End(true, LocalProfile.RewardPackSet(next, new LocalMatchRecord { Id = run.RunId, Seed = unchecked((int)run.Seed) }));
            LocalProfile.PayArenaReward(next);
        });
    }

    public ArenaView AcknowledgeReward(string runId)
    {
        var run = profile.ArenaRun;
        if (run == null && profile.ArenaReceiptSeen(runId)) return View();
        if (run?.Phase != ArenaPhase.RewardPending || run.RunId != runId) return Error("No matching Arena reward.");
        return Change(next => next.ArenaRun = null);
    }

    // Ready only. The plugin binds MatchStarter (LocalLadder.StartArenaMatch): it builds the drafted deck, picks the Arena NPC and
    // stages the native match. Builds without the plugin (unit tests) have none, so the start is refused with a reason.
    public bool StartMatch(out string error)
    {
        var run = profile.ArenaRun;
        if (run == null || run.Phase != ArenaPhase.Ready) { error = "No Ready Arena run."; return false; }
        if (profile.Pending != null) { error = "A match is pending."; return false; }
        if (profile.HasPendingRewardPresentation || profile.PackRevealPending) { error = "A reward presentation is pending."; return false; }
        var starter = ArenaMatchHook.MatchStarter;
        if (starter == null) { error = "Arena matches are not available here."; return false; }
        return starter(run, out error);
    }
}

internal delegate bool ArenaMatchStarter(ArenaRun run, out string error);

internal static class ArenaMatchHook
{
    internal static ArenaMatchStarter MatchStarter;   // set by the plugin when a save is bound
}

public sealed partial class LocalProfile
{
    internal const int MaximumArenaRequests = MaximumRecords;
    public ArenaRun ArenaRun => data.ArenaRun?.Copy();
    internal bool ArenaRequestSeen(string id) => data.ArenaEntryRequestIds.Contains(id);
    internal bool ArenaReceiptSeen(string id) => data.ArenaReceipts.Any(row => row.RunId == id);

    // Follow-up match integration calls this inside its existing CommitChange, alongside per-match XP.
    public static void SettleArenaMatch(LocalProfileData next, string result)
    {
        if (next.ArenaRun == null) throw new InvalidOperationException("No Arena run.");
        var run = next.ArenaRun;
        string pack = RewardPackSet(next, new LocalMatchRecord { Id = run.RunId, Seed = unchecked((int)run.Seed) });
        next.ArenaRun = run.SettleMatch(result, pack);
        if (next.ArenaRun.Phase == ArenaPhase.RewardPending) PayArenaReward(next);
    }

    internal static void PayArenaReward(LocalProfileData next)
    {
        var run = next.ArenaRun;
        if (run?.Phase != ArenaPhase.RewardPending || run.Reward == null || next.ArenaReceipts.Any(row => row.RunId == run.RunId))
            throw new InvalidOperationException("Arena reward already paid or not due.");
        next.GoldBalance = checked(next.GoldBalance + run.Reward.Gold);
        next.DustBalance = checked(next.DustBalance + run.Reward.Dust);
        foreach (string pack in run.Reward.PackTypes) AddPacks(next, pack, 1);
        next.ArenaReceipts.Add(new ArenaReceipt { RunId = run.RunId, EntryRequestId = run.EntryRequestId,
            RulesVersion = run.RulesVersion, GrindTier = run.GrindTier, Wins = run.Wins, Losses = run.Losses,
            Reward = ArenaRun.CopyReward(run.Reward), Retired = run.Retired });
        if (next.ArenaReceipts.Count > 20) next.ArenaReceipts.RemoveAt(0);
    }

    private static void ValidateArena(LocalProfileData next)
    {
        if (next.ArenaReceipts == null || next.ArenaEntryRequestIds == null || next.ArenaReceipts.Count > 20
            || next.ArenaEntryRequestIds.Count > MaximumArenaRequests || next.ArenaEntryRequestIds.Any(id => !ArenaRun.ValidRequest(id))
            || next.ArenaEntryRequestIds.Distinct(StringComparer.Ordinal).Count() != next.ArenaEntryRequestIds.Count
            || next.ArenaReceipts.Any(row => row == null)
            || next.ArenaReceipts.Select(row => row.RunId).Distinct(StringComparer.Ordinal).Count() != next.ArenaReceipts.Count
            || next.ArenaReceipts.Select(row => row.EntryRequestId).Distinct(StringComparer.Ordinal).Count() != next.ArenaReceipts.Count) ArenaRun.Invalid();
        foreach (var row in next.ArenaReceipts) {
            var rules = DraftRules.Arena(row.RulesVersion);
            if (!Guid.TryParseExact(row.RunId, "D", out _) || !next.ArenaEntryRequestIds.Contains(row.EntryRequestId)
                || !GrindTiers.IsKnown(row.GrindTier) || row.Wins < 0 || row.Wins > rules.MaxWins || row.Losses < 0 || row.Losses > rules.MaxLosses
                || row.Wins == rules.MaxWins && row.Losses == rules.MaxLosses
                || row.Retired && (row.Wins == rules.MaxWins || row.Losses == rules.MaxLosses)
                || !row.Retired && row.Wins != rules.MaxWins && row.Losses != rules.MaxLosses) ArenaRun.Invalid();
            ArenaRun.ValidateReward(row.Reward, row.RunId, row.Wins, row.GrindTier, row.RulesVersion);
        }
        var run = next.ArenaRun;
        if (run == null) return;
        run.Validate();
        if (!next.ArenaEntryRequestIds.Contains(run.EntryRequestId)) ArenaRun.Invalid();
        var receipt = next.ArenaReceipts.SingleOrDefault(row => row.RunId == run.RunId || row.EntryRequestId == run.EntryRequestId);
        if (run.Phase != ArenaPhase.RewardPending) { if (receipt != null) ArenaRun.Invalid(); }
        else if (receipt == null || receipt.RunId != run.RunId || receipt.EntryRequestId != run.EntryRequestId
            || receipt.Wins != run.Wins || receipt.Losses != run.Losses || receipt.Retired != run.Retired
            || receipt.GrindTier != run.GrindTier || receipt.RulesVersion != run.RulesVersion
            || receipt.Reward.Gold != run.Reward.Gold || receipt.Reward.Dust != run.Reward.Dust
            || !receipt.Reward.PackTypes.SequenceEqual(run.Reward.PackTypes)) ArenaRun.Invalid();
    }
}
