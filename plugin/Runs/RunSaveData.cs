using System;
using System.Collections.Generic;
using System.Linq;

public sealed class RunClassWinCount
{
    public string Class;
    public int Bosses, Runs;
}

// What the client wrote to the run mode's server save key, as a run operation.
public enum RunWriteKind { Ignore, ChooseLoot, ChooseTreasure, RunEndSeen, Retire }

public sealed class RunWrite
{
    public RunWriteKind Kind;
    // 0-based option for a choice (the client writes 1-based).
    public int Index;
}

// The run as the client's Dungeon Crawl display reads it: subkeys of the run mode's server save key (client enum
// GameSaveKeySubkeyId), and the meaning of the subkeys the client writes back. No client types: card and class ids come
// in through functions, so the CI suite `runs` checks the whole projection.
public static class RunSaveData
{
    public const int BossesDefeated = 13, DeckCardList = 14, DeckClass = 15, BossesFoughtList = 18, NextBossFight = 19,
        LootOptionA = 23, LootOptionB = 24, LootOptionC = 25, TreasureOption = 26, IsRunActive = 81, BossLostTo = 100,
        ChosenLoot = 101, ChosenTreasure = 102, NextBossHealth = 123, HeroHealth = 130, SeenRunComplete = 168,
        ScenarioId = 375, HeroCardDbId = 817, IsRunRetired = 662;

    // Per-class boss and run wins (the crowns of the class picker).
    public static readonly IReadOnlyDictionary<string, int[]> ClassWinSubkeys = new Dictionary<string, int[]> {
        { "PALADIN", new[] { 82, 83 } }, { "WARRIOR", new[] { 84, 85 } }, { "DRUID", new[] { 86, 87 } }, { "ROGUE", new[] { 88, 89 } },
        { "SHAMAN", new[] { 90, 91 } }, { "PRIEST", new[] { 92, 93 } }, { "WARLOCK", new[] { 94, 95 } }, { "HUNTER", new[] { 96, 97 } },
        { "MAGE", new[] { 98, 99 } }, { "DEMONHUNTER", new[] { 668, 669 } },
    };

    // Every subkey this projection owns, so a value it no longer sends is cleared, never left stale in the client.
    public static IEnumerable<int> Owned() => new[] { BossesDefeated, DeckCardList, DeckClass, BossesFoughtList, NextBossFight,
        LootOptionA, LootOptionB, LootOptionC, TreasureOption, IsRunActive, BossLostTo, ChosenLoot, ChosenTreasure, NextBossHealth,
        HeroHealth, SeenRunComplete, ScenarioId, HeroCardDbId, IsRunRetired }.Concat(ClassWinSubkeys.Values.SelectMany(v => v));

    // run: the stored run, or null before the first. dbf: a card's database id; classId: a class's TAG_CLASS value;
    // health: a hero card's Health.
    public static IReadOnlyDictionary<int, long[]> Project(RunDefinition definition, RunState run, IEnumerable<RunClassWinCount> wins,
        int playerHealth, Func<string, int> dbf, Func<string, int> classId, Func<string, int> health)
    {
        var values = Owned().ToDictionary(subkey => subkey, subkey => new long[0]);
        long[] Ids(IEnumerable<string> cards) => cards.Select(id => (long)dbf(id)).ToArray();
        long[] Heroes(IEnumerable<string> bosses) => Ids(bosses.Select(id => definition.Boss(id).Hero));
        foreach (RunClassWinCount win in wins ?? Enumerable.Empty<RunClassWinCount>())
            if (ClassWinSubkeys.TryGetValue(win.Class, out int[] keys))
            {
                values[keys[0]] = new long[] { win.Bosses };
                values[keys[1]] = new long[] { win.Runs };
            }
        // An ended run shows once (the client's run-end sequence); after it was seen the display starts over, as with no run.
        if (run != null && run.Status != RunStatus.Active && run.RunEndSeen) run = null;
        values[IsRunActive] = new long[] { run != null && run.Status == RunStatus.Active ? 1 : 0 };
        values[IsRunRetired] = new long[] { run != null && run.Status == RunStatus.Retired ? 1 : 0 };
        values[ChosenLoot] = new long[] { 0 };
        values[ChosenTreasure] = new long[] { 0 };
        if (run == null) return values;

        values[DeckClass] = new long[] { classId(run.Class) };
        values[HeroCardDbId] = new long[] { dbf(run.Hero) };
        // The deck list shows the passive treasures with the cards, as the client itself does when one is chosen; the fights
        // keep them apart (RunEncounter.Passives become the loadout).
        values[DeckCardList] = Ids(run.Deck.Concat(run.Passives));
        // The hero portrait's Health, on the run and on its end screen.
        values[HeroHealth] = new long[] { playerHealth };
        values[BossesDefeated] = Heroes(run.Defeated);
        values[BossesFoughtList] = Heroes(run.BossesFought);
        values[ScenarioId] = new long[] { definition.Client.Scenario };
        values[SeenRunComplete] = new long[] { run.RunEndSeen ? 1 : 0 };
        if (run.LostTo != null) values[BossLostTo] = new long[] { dbf(definition.Boss(run.LostTo).Hero) };
        if (run.Status == RunStatus.Active)
        {
            if (run.NextBoss != null)
            {
                RunBoss boss = definition.Boss(run.NextBoss);
                values[NextBossFight] = new long[] { dbf(boss.Hero) };
                values[NextBossHealth] = new long[] { health(boss.Hero) };
            }
            if (run.Offer != null && run.Offer.Kind == RunRewardKind.Loot)
            {
                int[] slots = { LootOptionA, LootOptionB, LootOptionC };
                for (int i = 0; i < run.Offer.Options.Count && i < slots.Length; i++)
                {
                    RunOption option = run.Offer.Options[i];
                    // [title card, cards...]: the client shows the title card's name over the bundle and adds the rest.
                    values[slots[i]] = new long[] { option.Title == null ? 0 : dbf(option.Title) }.Concat(Ids(option.Cards)).ToArray();
                }
            }
            else if (run.Offer != null)
                values[TreasureOption] = Ids(run.Offer.Options.Select(o => o.Cards[0]));
        }
        return values;
    }

    public static RunWrite Interpret(int subkey, IList<long> values)
    {
        long first = values != null && values.Count == 1 ? values[0] : 0;
        if (subkey == ChosenLoot && first > 0) return new RunWrite { Kind = RunWriteKind.ChooseLoot, Index = (int)first - 1 };
        if (subkey == ChosenTreasure && first > 0) return new RunWrite { Kind = RunWriteKind.ChooseTreasure, Index = (int)first - 1 };
        if (subkey == SeenRunComplete && first == 1) return new RunWrite { Kind = RunWriteKind.RunEndSeen };
        if (subkey == IsRunRetired && first == 1) return new RunWrite { Kind = RunWriteKind.Retire };
        return new RunWrite { Kind = RunWriteKind.Ignore };
    }
}
