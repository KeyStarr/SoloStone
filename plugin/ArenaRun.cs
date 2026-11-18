using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

public sealed class ArenaPick
{
    public List<string> Offers = new List<string>();
    public int Index;
    internal ArenaPick Copy() => new ArenaPick { Offers = Offers.ToList(), Index = Index };
}

public sealed class ArenaRun
{
    public string RunId;
    public long Seed;
    public int RulesVersion;
    public string GrindTier;
    public ArenaPhase Phase;
    public List<string> HeroOffers = new List<string>();
    public string Hero;
    public int HeroIndex = -1;
    public List<ArenaPick> Picks = new List<ArenaPick>();
    public List<string> Deck = new List<string>();
    // The offer of the pick in progress, stored when it is generated: a later card-pool or rules change never rerolls it.
    public List<string> PendingOffers = new List<string>();
    public bool ShouldSerializePendingOffers() => PendingOffers != null && PendingOffers.Count != 0;
    public int Wins, Losses;
    public int EntryGold;
    public string EntryRequestId;
    public ArenaReward Reward;
    public bool Retired;

    public ArenaRun Copy() => new ArenaRun {
        RunId = RunId, Seed = Seed, RulesVersion = RulesVersion, GrindTier = GrindTier, Phase = Phase,
        HeroOffers = HeroOffers.ToList(), Hero = Hero, HeroIndex = HeroIndex,
        Picks = Picks.Select(pick => pick.Copy()).ToList(), Deck = Deck.ToList(), PendingOffers = PendingOffers?.ToList() ?? new List<string>(), Wins = Wins, Losses = Losses,
        EntryGold = EntryGold, EntryRequestId = EntryRequestId, Reward = CopyReward(Reward), Retired = Retired
    };

    internal static ArenaReward CopyReward(ArenaReward reward) => reward == null ? null : new ArenaReward {
        RunId = reward.RunId, Wins = reward.Wins, Gold = reward.Gold, Dust = reward.Dust, PackTypes = reward.PackTypes.ToList()
    };

    // Pure transition. The caller credits Reward and stores this returned record in the same profile commit.
    public ArenaRun SettleMatch(string result, string packType)
    {
        Validate();
        if (Phase != ArenaPhase.Ready) throw new InvalidOperationException("Arena run is not ready.");
        var next = Copy();
        if (result == "Won") next.Wins++;
        else if (result == "Lost" || result == "Conceded" || result == "Abandoned") next.Losses++;
        else if (result != "Tied") throw new InvalidOperationException("Invalid Arena match result.");
        var rules = DraftRules.Arena(RulesVersion);
        if (next.Wins == rules.MaxWins || next.Losses == rules.MaxLosses) next.End(false, packType);
        next.Validate();
        return next;
    }

    internal void End(bool retired, string packType)
    {
        Retired = retired; Phase = ArenaPhase.RewardPending; PendingOffers = new List<string>();
        Reward = DraftRules.Arena(RulesVersion).Reward(RunId, Wins, GrindTier, packType);
    }

    // Structure only (the legendary-first-pick rule lives in the generator, never in Validate: a card-data rarity fix must not lock a save). Stored offers and rewards are history: they are never recomputed from today's card pool, rules or paces, so an
    // update that changes any of them can never make a saved run or receipt unloadable. Rules are read for constants only.
    internal void Validate()
    {
        var rules = DraftRules.Arena(RulesVersion);
        if (!Guid.TryParseExact(RunId, "D", out _) || !ValidRequest(EntryRequestId) || !GrindTiers.IsKnown(GrindTier)
            || EntryGold <= 0 || HeroOffers == null || Picks == null || Deck == null || PendingOffers == null
            || !ValidOffer(HeroOffers, rules.OfferSize) || Picks.Count > rules.DeckSize
            || Wins < 0 || Wins > rules.MaxWins || Losses < 0 || Losses > rules.MaxLosses
            || Wins == rules.MaxWins && Losses == rules.MaxLosses
            || Retired && (Wins == rules.MaxWins || Losses == rules.MaxLosses)
            || Phase == ArenaPhase.None || !Enum.IsDefined(typeof(ArenaPhase), Phase)) Invalid();
        if (Hero == null) {
            if (HeroIndex != -1 || Picks.Count != 0 || Deck.Count != 0
                || Phase != ArenaPhase.ChoosingHero && Phase != ArenaPhase.RewardPending) Invalid();
        } else if (HeroIndex < 0 || HeroIndex >= rules.OfferSize || Hero != HeroOffers[HeroIndex]
            || Phase == ArenaPhase.ChoosingHero) Invalid();
        if (Deck.Count != Picks.Count) Invalid();
        for (int i = 0; i < Picks.Count; i++) {
            var pick = Picks[i];
            if (pick == null || pick.Index < 0 || pick.Index >= rules.OfferSize || !ValidOffer(pick.Offers, rules.OfferSize)
                || Deck[i] != pick.Offers[pick.Index]) Invalid();
        }
        if (Phase == ArenaPhase.Drafting) {
            if (Hero == null || Picks.Count >= rules.DeckSize || !ValidOffer(PendingOffers, rules.OfferSize)) Invalid();
        } else if (PendingOffers.Count != 0) Invalid();
        if (Phase == ArenaPhase.Ready && Picks.Count != rules.DeckSize
            || Phase != ArenaPhase.RewardPending && (Reward != null || Retired || Wins == rules.MaxWins || Losses == rules.MaxLosses)
            || Picks.Count < rules.DeckSize && (Wins != 0 || Losses != 0)) Invalid();
        if (Phase == ArenaPhase.RewardPending) {
            if (!Retired && Wins != rules.MaxWins && Losses != rules.MaxLosses) Invalid();
            ValidateReward(Reward, RunId, Wins, GrindTier, RulesVersion);
        }
    }

    // Offers are three distinct card-id-shaped strings (no card lookup: a card retired later must not break a saved run).
    internal static bool ValidOffer(List<string> offers, int size) => offers != null && offers.Count == size
        && offers.All(id => id != null && id.Length > 0 && id.Length <= 64 && id.All(c => char.IsLetterOrDigit(c) || c == '_'))
        && offers.Distinct(StringComparer.Ordinal).Count() == offers.Count;

    internal static bool ValidRequest(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128 && !id.Any(char.IsControl);
    internal static void Invalid() => throw new InvalidOperationException("Invalid Arena run record.");
    // A paid reward is a receipt: amounts are history (paid under the table and pace of their day), so only shape and bounds are checked.
    internal static void ValidateReward(ArenaReward reward, string runId, int wins, string tier, int version)
    {
        var rules = DraftRules.Arena(version);
        if (reward == null || reward.PackTypes == null || reward.PackTypes.Count == 0 || reward.PackTypes.Count > 16
            || reward.PackTypes.Any(pack => string.IsNullOrEmpty(pack) || pack.Length > 64) || reward.RunId != runId || reward.Wins != wins
            || wins < 0 || wins > rules.MaxWins || reward.Gold < 0 || reward.Dust < 0 || reward.Gold > 10000000 || reward.Dust > 10000000) Invalid();
    }
}

public sealed class ArenaReceipt
{
    public string RunId;
    public string EntryRequestId;
    public int RulesVersion;
    public string GrindTier;
    public int Wins, Losses;
    public ArenaReward Reward;
    public bool Retired;
    internal ArenaReceipt Copy() => new ArenaReceipt { RunId = RunId, EntryRequestId = EntryRequestId,
        RulesVersion = RulesVersion, GrindTier = GrindTier, Wins = Wins, Losses = Losses,
        Reward = ArenaRun.CopyReward(Reward), Retired = Retired };
}
