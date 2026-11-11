using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B15FollowProof
{
    [GroupProof("b15-follow-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("CAP_002", Unpinned_Footsteps);
        StandardSetsProof.Case("CAP_101", Unpinned_Fuse);
        StandardSetsProof.Case("CAP_402", Unpinned_Evidence);
        StandardSetsProof.Case("CAP_802", Unpinned_Ghosts);
    }

    internal static void Answer(Game g, Controller p)
    {
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Count > 0, "Discover has offers");
        var view = g.GetPendingDecisions().Single();
        Core2026Proof.Check(g.TryAnswer(new DecisionAnswer(view.Id, view.ControllerId, new[] { p.Choice.Choices.First() }), out var error), "Discover accepted: " + error);
    }

    private static void Unpinned_Footsteps(Game g, Controller p, string id)
    {
        var filter = OfflinePoolFilter.Discover(type: CardType.MINION, keyword: GameTag.STEALTH);
        int size = OfflineCardPools.Candidates(g, p.BaseClass, filter, 127041).Count;
        Console.WriteLine("Follow Footsteps pool ROGUE=" + size);
        Core2026Proof.Check(size >= 3, "declared Stealth Discover bucket");
        Play(g, p, id);
        var offers = p.Choice.Choices.Select(i => g.IdEntityDic[i]).ToArray();
        Core2026Proof.Check(offers.Length == 3 && offers.Select(c => c.Card.Id).Distinct().Count() == 3
            && offers.All(c => c.Card.Type == CardType.MINION && c.Card[GameTag.STEALTH] > 0), "distinct Stealth offers");
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) }) {
            Answer(next, next.Player1);
            Core2026Proof.Check(next.Player1.HandZone.Single().AppliedEnchantments.Single().Card.Id == "CAP_002e",
                "Discover pending grant survives clone/snapshot");
        }
        var full = g.CloneExact(history: true);
        while (!full.Player1.HandZone.IsFull) Add(full.Player1, "CS2_231");
        Answer(full, full.Player1);
        Core2026Proof.Check(full.Player1.HandZone.Count == 10 && full.Player1.HandZone.All(c => c.AppliedEnchantments?.Count is null or 0)
            && full.Player1.Choice == null, "full hand burns selected card without a grant or stalled choice");
        Answer(g, p);
        var chosen = p.HandZone.Single();
        Core2026Proof.Check(chosen.AppliedEnchantments.Single().Card.Id == "CAP_002e", "selected card carries recursive grant");
        PlayEntity(g, p, chosen);
        foreach (var branch in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) }) {
            Answer(branch, branch.Player1);
            Core2026Proof.Check(branch.Player1.HandZone.Last().AppliedEnchantments.Single().Card.Id == "CAP_002e",
                "recursive Discover resumes after consumed grant through clone/snapshot");
        }
        Answer(g, p);
        Core2026Proof.Check(p.HandZone.Last().AppliedEnchantments.Single().Card.Id == "CAP_002e", "recursive Discover completes before regrant");
        End(g);
        Core2026Proof.Check(p.HandZone.All(c => c.AppliedEnchantments?.Count is null or 0), "Footsteps deadline");
    }

    private static void Unpinned_Fuse(Game g, Controller p, string id)
    {
        var pirate = Add(p, "CS2_146"); var unaffordable = Add(p, "CS2_146"); unaffordable.Cost = 10;
        var other = Add(p, "CS2_231"); var enemy = Add(p.Opponent, "CS2_146");
        var victim = Summon(p.Opponent, "CS2_200"); var source = Add(p, id);
        p.BaseMana = 2;
        var enemies = new ICharacter[] { p.Opponent.Hero, victim };
        var expectedDamage = enemies.Select(c => c.Damage).ToArray();
        var expectedRandom = g.Random.Clone();
        expectedDamage[expectedRandom.Next(2)] += 2;
        expectedRandom.Next(1); // Initial handoff has exactly one affordable Pirate.
        Core2026Proof.Check(g.Process(PlayCardTask.Spell(p, source)), "Fuse pays actual mana");
        Core2026Proof.Check(enemies.Select(c => c.Damage).SequenceEqual(expectedDamage),
            "Unpinned_Fuse: initial payload damages exact seeded enemy in hero/board order");
        Core2026Proof.Check(g.Random.CaptureState().Equals(expectedRandom.CaptureState()),
            "Unpinned_Fuse: initial payload consumes one enemy pick then one recipient pick");
        Core2026Proof.Check(p.UsedMana == 1
            && pirate.AppliedEnchantments.Single().Card.Id == "CAP_101e"
            && new[] { unaffordable, other, enemy }.All(c => c.AppliedEnchantments?.Count is null or 0),
            "Unpinned_Fuse: random enemy damage then affordable Pirate only");
        expectedDamage[expectedRandom.Next(2)] += 2;
        PlayEntity(g, p, pirate);
        Core2026Proof.Check(enemies.Select(c => c.Damage).SequenceEqual(expectedDamage),
            "Unpinned_Fuse: granted payload damages exact next seeded enemy");
        Core2026Proof.Check(g.Random.CaptureState().Equals(expectedRandom.CaptureState()),
            "Unpinned_Fuse: granted payload consumes one enemy pick, no eligible recipient");
    }

    private static void Unpinned_Evidence(Game g, Controller p, string id)
    {
        var bearer = Add(p, "CS2_231"); int count = p.Opponent.DeckZone.Count;
        Play(g, p, id);
        for (int i = 0; i < 3; i++) {
            var next = Add(p, "CS2_231");
            PlayEntity(g, p, bearer); bearer = next;
        }
        Core2026Proof.Check(p.Opponent.DeckZone.Count == count + 4 && bearer.AppliedEnchantments.Single().Card.Id == "CAP_402e",
            "Unpinned_Evidence: beyond two handoffs, one Imp per payload");
        var imp = p.Opponent.DeckZone.First(c => c.Card.Id == "CAP_400t2t");
        Core2026Proof.Check(imp.Card.AssetId == 127024 && imp.Card.ATK == 3 && imp.Card.Health == 3
            && imp[GameTag.LIFESTEAL] == 1, "reuse exact exported Imp-formant");
    }

    private static void Unpinned_Ghosts(Game g, Controller p, string id)
    {
        // Affordable targetless Execute remains eligible, despite normal play requirements.
        var execute = Add(p, "CS2_108");
        Play(g, p, id);
        var ghost = p.BoardZone.Single();
        Core2026Proof.Check(ghost.Card.Id == "CAP_802t" && ghost.Card.AssetId == 127063 && ghost.AttackDamage == 2
            && ghost.Health == 1 && ghost[GameTag.REBORN] == 1 && ghost.IsRace(Race.UNDEAD)
            && execute.AppliedEnchantments.Single().Card.Id == "CAP_802e1", "Unpinned_Ghosts: exact token and affordability-only recipient");
        Kill(g, ghost);
        Core2026Proof.Check(p.BoardZone.Single().Card.Id == "CAP_802t" && p.BoardZone.Single()[GameTag.REBORN] == 0,
            "Ghost genuinely Reborns once");
        while (!p.BoardZone.IsFull) Summon(p, "CS2_231");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && execute.AppliedEnchantments.Count == 2,
            "full board fizzles summon but still hands off, independent grants stack");
    }
}
