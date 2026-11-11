using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class SanguineInfestationProof
{
    // data/rules-evidence/tie-break/{lowest-health-hero-pool,leech-health-transfer,lowest-health-ties}.yaml (A/C/D; see Husk proof).
    internal static void Run() => StandardSetsProof.Case("EDR_817", Unpinned_SanguineInfestation);
    private static void Unpinned_SanguineInfestation(Game g, Controller p, string id)
    {
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182");
        int deck = p.DeckZone.Count;
        var source = Play(g, p, id);
        Core2026Proof.Check(g.PowerHistory.Full.OfType<PowerHistoryMetaData>().Any(m => m.Type == MetaDataType.TARGET && m.Info.Contains(source.Id)), "authored draw POWER targets revealed source actor");
        var leeches = p.BoardZone.Where(m => m.Card.Id == "EDR_810t").ToArray();
        Core2026Proof.Check(leeches.Length == 2 && leeches.All(m => m.AttackDamage == 0 && m.Health == 2)
            && leeches[0].ZonePosition == 2, "exact Leech count/stats, appends after location");
        Core2026Proof.Check(p.HandZone.Count == 2 && p.DeckZone.Count == deck - 2, "draws two"); PoolPilotProof.FillHand(p); int before = p.DeckZone.Count; PoolPilotProof.Resolve(g, source); Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == before - 2, "full-hand burns both draws");
        PoolPilotProof.EmptyHand(p); StandardSetsProof.ParkBoard(p); LocationProofs.Location(p);
        while (p.BoardZone.FreeSpace > 1) Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "EDR_810t") == 1, "one physical slot truncates summons");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.SlotCount == 7, "full board stays bounded");
        StandardSetsProof.ParkBoard(p); HideousHuskProof.ExerciseLeech(g, p);
        var empty = DeathKnightC1Proof.NewGame(g.Seed); PoolPilotProof.ClassDeck(empty.Player1);
        Play(empty, empty.Player1, id);
        Core2026Proof.Check(empty.Player1.Hero.Fatigue == 2 && empty.Player1.Hero.Health == 27
            && empty.Player1.BoardZone.Count(m => m.Card.Id == "EDR_810t") == 2, "empty deck fatigues twice then still summons");
    }
}
