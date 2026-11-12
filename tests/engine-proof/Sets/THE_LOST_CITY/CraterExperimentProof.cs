using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

// Tier D: pinned 37.0.0.254477 export; existing minion-type Kindred primitive.
internal static class CraterExperimentProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_435", KindredCopy, legacyPoolMember: true);

    internal static void KindredCopy(Game g, Controller p, string id)
    {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 1, "no previous-turn type means no copy");
        Core2026Proof.Check(id == "DINO_435" ? source.IsRace(Race.ALL) && source.AttackDamage == 3 && source.Health == 4
            : source.IsRace(Race.ELEMENTAL) && source.AttackDamage == 2 && source.Health == 2 && source.HasDeathrattle, "printed types/stats/Deathrattle");
        ParkBoard(p);
        Play(g, p, "CS2_033"); End(g); End(g);
        ParkBoard(p);
        var location = LocationProofs.Location(p);
        var right = Summon(p, "CS2_182");
        source = PlayAt(g, p, id, 1);
        var copy = p.BoardZone.Single(m => m.Card.Id == id && m.Id != source.Id);
        Core2026Proof.Check(p.BoardZone.Slots.Select(e => e.Id).SequenceEqual(new[] { location.Id, source.Id, copy.Id, right.Id }),
            "Kindred copy immediately right of source across physical slots; no recursive battlecry");
        Core2026Proof.Check(copy.Card.Id == id && copy.AttackDamage == source.AttackDamage && copy.Health == source.Health
            && copy.HasDeathrattle == source.HasDeathrattle, "copy identity/stats/Deathrattle");
        ParkBoard(p);
        LocationProofs.Location(p);
        for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 1, "last mixed board slot prevents copy");
        ParkBoard(p); End(g); End(g); End(g);
        Play(g, p.Opponent, "CS2_033"); End(g);
        Play(g, p, "CS2_033"); ParkBoard(p); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 1, "opponent previous-turn and own current-turn type do not activate Kindred");
        ParkBoard(p); End(g); End(g); End(g); End(g);
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 1, "stale type no longer activates Kindred");
        ParkBoard(p); End(g); End(g); PoolPilotProof.EmptyHand(p);
        Play(g, p, "CS2_171"); End(g); End(g); ParkBoard(p);
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == (id == "DINO_435" ? 2 : 1), "All matches Beast; Elemental does not match Beast");
    }
}
