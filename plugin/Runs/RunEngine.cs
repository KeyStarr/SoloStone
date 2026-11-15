using System;
using System.Collections.Generic;
using System.Linq;

// What the next boss fight needs, read from a run between fights.
public sealed class RunEncounter
{
    public RunBoss Boss;
    public int Round, PlayerHealth, BossStartingMana;
    public string Class, Hero;
    public IReadOnlyList<string> Deck, Passives;
}

// The rules of a run: pure transitions over RunState, driven by an admitted RunDefinition. Only admitted content is ever
// offered or fought. Every random choice draws from StableSeed over (run seed, step, purpose), so the same seed and the
// same choices give the same run.
public sealed class RunEngine
{
    private const long BossDraw = 0x424F5353, OfferDraw = 0x4F464652, BundleDraw = 0x42554E44;

    public RunDefinition Definition { get; private set; }
    public RunAdmission Admission { get; private set; }

    public RunEngine(RunAdmission admission)
    {
        Admission = admission ?? throw new ArgumentNullException(nameof(admission));
        Definition = admission.Definition;
    }

    public RunState Start(string cls, string hero, long seed)
    {
        if (!Admission.IsReady) throw new InvalidOperationException(Definition.Id + " is not ready: " + string.Join("; ", Admission.Problems()));
        RunClassStart start = Definition.ClassStart(cls) ?? throw new ArgumentException(Definition.Id + " has no class " + cls);
        if (string.IsNullOrEmpty(hero)) throw new ArgumentException("A run needs a hero card.");
        var state = new RunState {
            Definition = Definition.Id, DefinitionVersion = Definition.Version, Class = cls, Hero = hero, Seed = seed,
            Status = RunStatus.Active, Deck = start.Deck.ToList(),
        };
        state.NextBoss = PickBoss(state);
        state.BossesFought.Add(state.NextBoss);
        return state;
    }

    public int PlayerHealth(RunState state) => Definition.BaseHealth + Definition.HealthPerWin * state.BossesDefeated;

    public RunEncounter NextEncounter(RunState state)
    {
        RequireBetweenFights(state);
        if (state.Offer != null) throw new InvalidOperationException("Choose the pending reward before the next fight.");
        RunBoss boss = Definition.Boss(state.NextBoss);
        return new RunEncounter {
            Boss = boss, Round = state.Round, PlayerHealth = PlayerHealth(state),
            BossStartingMana = Definition.Round(state.Round).BossStartingMana,
            Class = state.Class, Hero = state.Hero, Deck = state.Deck.ToList(), Passives = state.Passives.ToList(),
        };
    }

    public void ResolveFight(RunState state, RunFightResult result)
    {
        RequireBetweenFights(state);
        if (state.Offer != null) throw new InvalidOperationException("No fight is pending while a reward is offered.");
        switch (result)
        {
            case RunFightResult.Interrupted:
                return;
            case RunFightResult.Tied:
                // A tie is fought again against another boss of the same round, when there is one.
                state.NextBoss = PickBoss(state);
                state.BossesFought.Add(state.NextBoss);
                return;
            case RunFightResult.Lost:
                state.Status = RunStatus.Lost;
                state.LostTo = state.NextBoss;
                state.NextBoss = null;
                return;
            case RunFightResult.Won:
                // The defeated boss is behind the run: no next boss is known until the rewards are chosen.
                state.BossesDefeated++;
                state.NextBoss = null;
                if (state.BossesDefeated == Definition.Rounds.Count)
                {
                    state.Status = RunStatus.Won;
                    return;
                }
                state.PendingRewards = Definition.Round(state.BossesDefeated).Rewards.ToList();
                Advance(state);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(result));
        }
    }

    // index is 0-based (the client's PLAYER_CHOSEN_* values are 1-based; the save-data bridge converts).
    public void Choose(RunState state, int index)
    {
        RequireBetweenFights(state);
        RunOffer offer = state.Offer ?? throw new InvalidOperationException("No reward is offered.");
        if (index < 0 || index >= offer.Options.Count) throw new ArgumentOutOfRangeException(nameof(index));
        RunOption option = offer.Options[index];
        if (offer.Kind == RunRewardKind.PassiveTreasure) state.Passives.AddRange(option.Cards);
        else state.Deck.AddRange(option.Cards);
        state.Offer = null;
        Advance(state);
    }

    public void Retire(RunState state)
    {
        RequireBetweenFights(state);
        state.Status = RunStatus.Retired;
        state.Offer = null;
        state.PendingRewards.Clear();
        state.NextBoss = null;
    }

    public void MarkRunEndSeen(RunState state)
    {
        if (state.Status == RunStatus.Active) throw new InvalidOperationException("The run has not ended.");
        state.RunEndSeen = true;
    }

    private void RequireBetweenFights(RunState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (state.Definition != Definition.Id || state.DefinitionVersion != Definition.Version)
            throw new InvalidOperationException("Run " + state.Definition + " v" + state.DefinitionVersion + " is not " + Definition.Id + " v" + Definition.Version);
        if (state.Status != RunStatus.Active) throw new InvalidOperationException("The run is over (" + state.Status + ").");
    }

    // Offers the next pending reward that has options; when none is left, picks the next boss.
    private void Advance(RunState state)
    {
        while (state.PendingRewards.Count > 0)
        {
            RunRewardKind kind = state.PendingRewards[0];
            state.PendingRewards.RemoveAt(0);
            RunOffer offer = BuildOffer(state, kind);
            if (offer.Options.Count == 0) continue;
            state.Offer = offer;
            return;
        }
        state.NextBoss = PickBoss(state);
        state.BossesFought.Add(state.NextBoss);
    }

    private RunOffer BuildOffer(RunState state, RunRewardKind kind)
    {
        var offer = new RunOffer { Kind = kind };
        Random random = Draw(state, OfferDraw);
        if (kind == RunRewardKind.Loot)
        {
            var buckets = Definition.Buckets.Where(b => b.Class == state.Class && Admission.Buckets.Contains(b.Id)).ToList();
            foreach (RunBucket bucket in Take(buckets, RunAdmission.OfferSize, random))
            {
                Random cards = Draw(state, BundleDraw);
                var option = new RunOption { Title = bucket.Title };
                // A bundle draws with replacement: the same card can appear more than once.
                for (int i = 0; i < RunAdmission.OfferSize; i++) option.Cards.Add(bucket.Cards[cards.Next(bucket.Cards.Count)]);
                offer.Options.Add(option);
            }
            return offer;
        }
        IEnumerable<string> pool = kind == RunRewardKind.PassiveTreasure
            ? Definition.PassiveTreasures.Where(t => Admission.PassiveTreasures.Contains(t) && !state.Passives.Contains(t))
            : Definition.ActiveTreasures.Where(t => Admission.ActiveTreasures.Contains(t) && !state.Deck.Contains(t));
        foreach (string treasure in Take(pool.ToList(), RunAdmission.OfferSize, random))
            offer.Options.Add(new RunOption { Cards = { treasure } });
        return offer;
    }

    // A boss of the current round whose family was not fought yet this run; if every family was, any boss of the round
    // but the one just faced (a tie in a round with one boss faces it again).
    private string PickBoss(RunState state)
    {
        var round = Definition.Bosses.Where(b => b.Rounds.Contains(state.Round) && Admission.Bosses.Contains(b.Id)).Select(b => b.Id).ToList();
        var foughtFamilies = new HashSet<string>(state.BossesFought.Select(id => Definition.Boss(id)?.Family ?? id), StringComparer.Ordinal);
        var fresh = round.Where(id => !foughtFamilies.Contains(Definition.Boss(id).Family)).ToList();
        var candidates = fresh.Count > 0 ? fresh : round.Where(id => id != state.NextBoss).ToList();
        if (candidates.Count == 0) candidates = round;
        if (candidates.Count == 0) throw new InvalidOperationException(Definition.Id + ": round " + state.Round + " has no admitted boss.");
        return candidates[Draw(state, BossDraw).Next(candidates.Count)];
    }

    private static Random Draw(RunState state, long purpose) => new Random(StableSeed.Derive(state.Seed, state.Step++, purpose));

    // Up to count distinct items in a random order (partial Fisher-Yates over a copy).
    private static List<T> Take<T>(List<T> items, int count, Random random)
    {
        var copy = items.ToList();
        int take = Math.Min(count, copy.Count);
        for (int i = 0; i < take; i++)
        {
            int j = i + random.Next(copy.Count - i);
            T swap = copy[i]; copy[i] = copy[j]; copy[j] = swap;
        }
        return copy.Take(take).ToList();
    }
}
