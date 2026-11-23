using System.Collections.Generic;

// Arena MVP (#6): the contract between the run state (profile side, ArenaRun*.cs) and the native Draft screens (NativeArena*.cs).
// One mode: 12 wins / 3 losses, no redraft, no rating. The native layer never touches the profile directly; it only calls this
// service and turns ArenaView into the client's Draft/Arena replies. Every mutating call is one atomic profile commit and returns
// the state after it. A call that is not allowed in the current phase changes nothing and returns Error (never throws for that).
public enum ArenaPhase
{
    None,          // no run: Begin may start one
    ChoosingHero,  // entry paid, 3 hero offers shown (slot 0)
    Drafting,      // hero chosen, picks 1..30 in progress (first pick: legendaries)
    Ready,         // 30 cards drafted, between matches
    RewardPending, // run ended (12 wins, 3 losses or retire): reward already paid into the save, presentation not yet acknowledged
}

public sealed class ArenaReward
{
    public string RunId;
    public int Wins;
    public int Gold;                 // paid, pace-scaled
    public int Dust;                 // paid, pace-scaled
    public List<string> PackTypes = new List<string>(); // paid unopened packs, one entry per pack (e.g. "STANDARD")
}

public sealed class ArenaView
{
    public ArenaPhase Phase;
    public string RunId;             // null when Phase == None
    public int Wins, Losses;
    public int MaxWins = 12, MaxLosses = 3;
    public int Slot;                 // slot of the pending choice: 0 = hero choice, 1..30 = the card pick being offered; 30 when Ready
    public int MaxSlot = 30;
    public string HeroCardId;        // chosen hero (e.g. "HERO_08"), null before the hero pick
    public List<string> HeroOffers = new List<string>();  // 3 hero card ids while ChoosingHero
    public List<string> Offers = new List<string>();      // 3 card ids for the current pick while Drafting
    public List<string> Deck = new List<string>();        // drafted card ids in pick order
    public ArenaReward Reward;       // set while RewardPending
    public List<ArenaReward> RewardTable = new List<ArenaReward>(); // reward per win count 0..MaxWins at the current pace (preview)
    public int EntryCost;            // 150
    public long GoldBalance;
    public string Error;             // null on success; a short reason when the call was refused
}

public interface IArenaService
{
    ArenaView View();
    ArenaView Begin(string requestId);           // pays the entry; idempotent per requestId
    ArenaView Choose(int slot, int index);       // slot must equal View().Slot; index 0..2; a repeat of the last choice is a no-op
    ArenaView Retire();                          // ends the run now, pays the reward for the wins so far
    ArenaView AcknowledgeReward(string runId);   // closes the reward presentation; Phase -> None
    bool StartMatch(out string error);           // Ready only: starts a real match with the drafted deck against an Arena NPC
}

internal static class ArenaServiceRegistry
{
    internal static IArenaService Current = null; // set when a save is bound, like CraftingLedgerRegistry
}
