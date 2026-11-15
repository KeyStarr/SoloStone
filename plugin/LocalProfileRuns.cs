using System;
using System.Collections.Generic;
using System.Linq;

// A boss fight under way: its id (settled with ResolveRunFight) and what to stage.
public sealed class LocalRunFight
{
    public string Id;
    public RunEncounter Encounter;
}

// Run modes on the save (plugin/Runs drives the rules; LocalProfile.cs stores LocalRunSlot). Each operation is one commit:
// the run, the fight marker and the class tallies move together or not at all. None of them touches the economy.
// Plugin-only half, like LocalProfileNpcWorld.cs: the test projects that link LocalProfile.cs do not compile plugin/Runs.
public sealed partial class LocalProfile
{
    // The stored run of this mode, when it belongs to this definition (same version, every boss and the class still in
    // it); else null. A run saved by an older definition cannot be continued.
    internal RunState RunOf(RunDefinition definition)
    {
        var slot = data.Runs?.FirstOrDefault(s => s.Definition == definition.Id);
        return slot?.Run == null ? null : Usable(definition, slot.Run);
    }

    internal bool RunFightUnderway(RunDefinition definition) => data.Runs?.Any(s => s.Definition == definition.Id && s.FightId != null) == true;

    internal IReadOnlyList<LocalRunClassWins> RunWins(RunDefinition definition) =>
        (data.Runs?.FirstOrDefault(s => s.Definition == definition.Id)?.Wins ?? new List<LocalRunClassWins>())
            .Select(w => new LocalRunClassWins { Class = w.Class, Bosses = w.Bosses, Runs = w.Runs }).ToList();

    // A new run. Refused while a ladder match or a run fight is pending, or while this mode's run is still active.
    internal RunState StartRun(RunEngine engine, string cls, string hero, long seed) => CommitChange(next =>
    {
        RequireIdle(next);
        LocalRunSlot slot = Slot(next, engine.Definition.Id, true);
        RunState current = slot.Run == null ? null : Usable(engine.Definition, slot.Run);
        if (current != null && current.Status == RunStatus.Active) throw new InvalidOperationException("A run of " + engine.Definition.Id + " is already active.");
        RunState state = engine.Start(cls, hero, seed);
        slot.Run = ToRecord(state);
        return state;
    });

    // Marks the next boss fight as under way and returns what to stage; the match's result goes to ResolveRunFight.
    internal LocalRunFight BeginRunFight(RunEngine engine) => CommitChange(next =>
    {
        RequireIdle(next);
        LocalRunSlot slot = Slot(next, engine.Definition.Id, false);
        RunState state = slot?.Run == null ? null : Usable(engine.Definition, slot.Run);
        if (state == null) throw new InvalidOperationException("No run of " + engine.Definition.Id + " to continue.");
        RunEncounter encounter = engine.NextEncounter(state);
        slot.FightId = Guid.NewGuid().ToString();
        return new LocalRunFight { Id = slot.FightId, Encounter = encounter };
    });

    // The fight's result: the run moves on (or ends), the marker clears and a win counts for the run's class.
    internal RunState ResolveRunFight(RunEngine engine, string fightId, RunFightResult result) => CommitChange(next =>
    {
        LocalRunSlot slot = Slot(next, engine.Definition.Id, false);
        if (slot == null || slot.FightId == null || slot.FightId != fightId) throw new InvalidOperationException("No matching run fight.");
        RunState state = Usable(engine.Definition, slot.Run) ?? throw new InvalidOperationException("The run under way no longer fits its definition.");
        engine.ResolveFight(state, result);
        if (result == RunFightResult.Won)
        {
            LocalRunClassWins wins = slot.Wins.FirstOrDefault(w => w.Class == state.Class);
            if (wins == null) slot.Wins.Add(wins = new LocalRunClassWins { Class = state.Class });
            wins.Bosses++;
            if (state.Status == RunStatus.Won) wins.Runs++;
        }
        slot.FightId = null;
        slot.Run = ToRecord(state);
        return state;
    });

    internal RunState ChooseRunReward(RunEngine engine, int index) => ChangeRun(engine, state => engine.Choose(state, index));
    internal RunState RetireRun(RunEngine engine) => ChangeRun(engine, engine.Retire);
    internal RunState MarkRunEndSeen(RunEngine engine) => ChangeRun(engine, engine.MarkRunEndSeen);

    internal IReadOnlyList<LocalRunFlag> RunClientFlags(RunDefinition definition) =>
        (data.Runs?.FirstOrDefault(s => s.Definition == definition.Id)?.ClientFlags ?? new List<LocalRunFlag>())
            .Select(f => new LocalRunFlag { Subkey = f.Subkey, Values = f.Values.ToList() }).ToList();

    // Keeps what the client wrote for the mode (voice lines and tutorials it has shown); an empty list removes the flag.
    internal void SetRunClientFlag(RunDefinition definition, int subkey, IList<long> values) => CommitChange(next =>
    {
        if (subkey <= 0 || values == null || values.Count > 512) throw new ArgumentException("Invalid run client flag.");
        LocalRunSlot slot = Slot(next, definition.Id, true);
        slot.ClientFlags.RemoveAll(f => f.Subkey == subkey);
        if (values.Count > 0) slot.ClientFlags.Add(new LocalRunFlag { Subkey = subkey, Values = values.ToList() });
        slot.ClientFlags.Sort((a, b) => a.Subkey.CompareTo(b.Subkey));
        return 0;
    });

    private RunState ChangeRun(RunEngine engine, Action<RunState> change) => CommitChange(next =>
    {
        LocalRunSlot slot = Slot(next, engine.Definition.Id, false);
        if (slot?.FightId != null) throw new InvalidOperationException("A run fight is under way.");
        RunState state = slot?.Run == null ? null : Usable(engine.Definition, slot.Run);
        if (state == null) throw new InvalidOperationException("No run of " + engine.Definition.Id + ".");
        change(state);
        slot.Run = ToRecord(state);
        return state;
    });

    private static void RequireIdle(LocalProfileData profile)
    {
        if (profile.Pending != null) throw new InvalidOperationException("A local match is already pending.");
        if (RunFightUnderway(profile)) throw new InvalidOperationException("A run fight is under way.");
    }

    private static LocalRunSlot Slot(LocalProfileData profile, string definition, bool create)
    {
        LocalRunSlot slot = profile.Runs.FirstOrDefault(s => s.Definition == definition);
        if (slot == null && create) profile.Runs.Add(slot = new LocalRunSlot { Definition = definition });
        return slot;
    }

    private static RunState Usable(RunDefinition definition, LocalRunRecord run)
    {
        if (run.DefinitionVersion != definition.Version || definition.ClassStart(run.Class) == null) return null;
        if (run.BossesFought.Concat(new[] { run.NextBoss, run.LostTo }).Any(id => id != null && definition.Boss(id) == null)) return null;
        try { return ToState(definition.Id, run); }
        catch (ArgumentException) { return null; }
    }

    private static RunState ToState(string definition, LocalRunRecord run) => new RunState {
        Definition = definition, DefinitionVersion = run.DefinitionVersion, Class = run.Class, Hero = run.Hero, Seed = run.Seed,
        Step = run.Step, Status = (RunStatus)Enum.Parse(typeof(RunStatus), run.Status), Deck = run.Deck.ToList(),
        Passives = run.Passives.ToList(), BossesFought = run.BossesFought.ToList(), BossesDefeated = run.BossesDefeated,
        NextBoss = run.NextBoss, LostTo = run.LostTo,
        PendingRewards = run.PendingRewards.Select(kind => (RunRewardKind)Enum.Parse(typeof(RunRewardKind), kind)).ToList(),
        Offer = run.Offer == null ? null : new RunOffer {
            Kind = (RunRewardKind)Enum.Parse(typeof(RunRewardKind), run.Offer.Kind),
            Options = run.Offer.Options.Select(o => new RunOption { Title = o.Title, Cards = o.Cards.ToList() }).ToList() },
        RunEndSeen = run.RunEndSeen,
    };

    private static LocalRunRecord ToRecord(RunState state) => new LocalRunRecord {
        DefinitionVersion = state.DefinitionVersion, Class = state.Class, Hero = state.Hero, Seed = state.Seed, Step = state.Step,
        Status = state.Status.ToString(), Deck = state.Deck.ToList(), Passives = state.Passives.ToList(),
        BossesFought = state.BossesFought.ToList(), BossesDefeated = state.BossesDefeated, NextBoss = state.NextBoss, LostTo = state.LostTo,
        PendingRewards = state.PendingRewards.Select(kind => kind.ToString()).ToList(),
        Offer = state.Offer == null ? null : new LocalRunOffer { Kind = state.Offer.Kind.ToString(),
            Options = state.Offer.Options.Select(o => new LocalRunOption { Title = o.Title, Cards = o.Cards.ToList() }).ToList() },
        RunEndSeen = state.RunEndSeen,
    };
}
