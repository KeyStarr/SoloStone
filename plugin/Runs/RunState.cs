using System.Collections.Generic;
using System.Linq;

public enum RunStatus { Active, Won, Lost, Retired }

// How a boss fight ended, as the run sees it. Interrupted: the game closed mid-fight; the same boss is fought again.
public enum RunFightResult { Won, Lost, Tied, Interrupted }

public sealed class RunOption
{
    // Loot: the bucket's title card and its cards. Treasures: the treasure card alone, no title.
    public string Title;
    public List<string> Cards = new List<string>();

    public RunOption Clone() => new RunOption { Title = Title, Cards = Cards.ToList() };
}

public sealed class RunOffer
{
    public RunRewardKind Kind;
    public List<RunOption> Options = new List<RunOption>();

    public RunOffer Clone() => new RunOffer { Kind = Kind, Options = Options.Select(o => o.Clone()).ToList() };
}

// One run in progress or finished. Mutated only by RunEngine; plain fields so the profile can store it.
public sealed class RunState
{
    public string Definition;
    public int DefinitionVersion;
    public string Class, Hero;
    public long Seed;
    // Random draws taken so far: each draw derives its own seed from (Seed, Step), so a run replays exactly.
    public int Step;
    public RunStatus Status;
    public List<string> Deck = new List<string>();
    public List<string> Passives = new List<string>();
    public List<string> BossesFought = new List<string>();
    public int BossesDefeated;
    public string NextBoss, LostTo;
    // Rewards still to offer after the last win (the current offer is not in it).
    public List<RunRewardKind> PendingRewards = new List<RunRewardKind>();
    public RunOffer Offer;
    public bool RunEndSeen;

    public int Round => BossesDefeated + 1;

    public RunState Clone() => new RunState {
        Definition = Definition, DefinitionVersion = DefinitionVersion, Class = Class, Hero = Hero, Seed = Seed, Step = Step,
        Status = Status, Deck = Deck.ToList(), Passives = Passives.ToList(), BossesFought = BossesFought.ToList(),
        BossesDefeated = BossesDefeated, NextBoss = NextBoss, LostTo = LostTo, PendingRewards = PendingRewards.ToList(),
        Offer = Offer?.Clone(), RunEndSeen = RunEndSeen,
    };
}
