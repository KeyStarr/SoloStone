using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class CompanionProof
{
    private static string[] Roster(Controller p) => CompanionRules.Roster(p);
    private static void Upgrade(Controller p, int step)
    {
        var source = Entity.FromCard(p, Cards.FromId("MEND_300"), zone: p.SetasideZone);
        CompanionRules.Upgrade(p, source, step);
    }

    internal static void Run()
    {
        var g = ImbueProof.Make(CardClass.HUNTER); var p = g.Player1;
        Core2026Proof.Check(Roster(p).SequenceEqual(new[] { "NEW1_032", "NEW1_033", "NEW1_034" }), "default companion order");
        Upgrade(p, 1);
        var first = Roster(p);
        Core2026Proof.Check(first.Length == 3 && first.All(id => Cards.FromId(id).Cost == 4 && Cards.FromId(id).IsRace(Race.BEAST)),
            "upgrade stores three admitted four-Cost Beasts");
        Core2026Proof.Check(Roster(p.Opponent).SequenceEqual(new[] { "NEW1_032", "NEW1_033", "NEW1_034" }), "other controller untouched");
        var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
        foreach (var copy in new[] { clone, loaded, g })
        {
            Core2026Proof.Check(Roster(copy.Player1).SequenceEqual(first), "clone/snapshot preserves roster");
            Play(copy, copy.Player1, "CORE_NEW1_031");
            Core2026Proof.Check(first.Contains(copy.Player1.BoardZone.Single().Card.Id), "Animal Companion consumes persistent roster");
            Upgrade(copy.Player1, 1);
            Core2026Proof.Check(Roster(copy.Player1).All(id => Cards.FromId(id).Cost == 5), "successive upgrades add to previous cost");
            Upgrade(copy.Player1, 2);
            Core2026Proof.Check(Roster(copy.Player1).All(id => Cards.FromId(id).Cost == 7), "two-step reaches seven");
            Upgrade(copy.Player1, 20);
            var cappedRng = copy.Random.CaptureState();
            Upgrade(copy.Player1, 1);
            Core2026Proof.Check(!copy.Random.CaptureState().Equals(cappedRng),
                "Unpinned_CompanionCap: capped upgrade samples again even with a singleton pool");
            Core2026Proof.Check(Roster(copy.Player1).Length == 3 && Roster(copy.Player1).All(id => Cards.FromId(id).Cost == 10),
                "Unpinned_CompanionCap: cap rerolls three complete ten-Cost slots");
        }
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(loaded),
            "future state/RNG equal after clone and snapshot");
        var roster = Roster(p);
        p.AddHeroAndPower(Cards.HeroCard(CardClass.MAGE));
        Core2026Proof.Check(Roster(p).SequenceEqual(roster), "hero and power replacement preserve player roster");
        StandardSetsProof.ParkBoard(p);
        Play(g, p, "CORE_OG_211");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Card.Id).SequenceEqual(roster.Reverse()), "Call of the Wild reads all roster slots in original summon order");
        StandardSetsProof.ParkBoard(p);
        Play(g, p, "MEND_301");
        Core2026Proof.Check(p.Choice.Choices.Select(i => g.IdEntityDic[i].Card.Id).SequenceEqual(roster), "Spiritspeaker reads upgraded roster");
        PlayerDecisionProof.Answer(g, p.Choice.Choices[0]);
        StandardSetsProof.ParkBoard(p);
        Play(g, p, "LOOT_217");
        Core2026Proof.Check(p.BoardZone.Count == 1 && roster.Contains(p.BoardZone.Single().Card.Id), "To My Side reads upgraded roster");
        StandardSetsProof.ParkBoard(p);
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        Play(g, p, "LOOT_217");
        Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.All(m => roster.Contains(m.Card.Id)), "To My Side picks distinct slots even with duplicate identities");
        StandardSetsProof.ParkBoard(p);
        Play(g, p, "EDR_853");
        Play(g, p, "GAME_005");
        Core2026Proof.Check(p.BoardZone.Count == 2 && roster.Contains(p.BoardZone.Last().Card.Id), "Broll reads upgraded roster");
        var replay = ImbueProof.Make(CardClass.HUNTER);
        CompanionRules.Upgrade(replay.Player1, replay.Player1.Hero, 1, Cards.FromId("MEND_303").AssetId);
        Core2026Proof.Check(Roster(replay.Player1).All(id => Cards.FromId(id).Cost == 4), "copied Battlecry retains its declared pool recipe identity");
        var before = Roster(p); Upgrade(clone.Player1, 1);
        Core2026Proof.Check(Roster(p).SequenceEqual(before), "clone upgrades cannot mutate parent");
        Console.WriteLine("PASS companion facility: default, persistence, cumulative cost, cap, clone/snapshot, consumers");
    }
}
