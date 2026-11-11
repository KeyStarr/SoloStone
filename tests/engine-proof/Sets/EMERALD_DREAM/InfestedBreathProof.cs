using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class InfestedBreathProof
{
    // data/rules-evidence/tie-break/{lowest-health-hero-pool,leech-health-transfer,lowest-health-ties}.yaml (A/C/D; see Husk proof).
    internal static void Run() => StandardSetsProof.Case("EDR_814", Unpinned_InfestedBreath);
    private static void Unpinned_InfestedBreath(Game g, Controller p, string id)
    {
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_182");
        var source = Play(g, p, id, enemy);
        var leeches = p.BoardZone.Where(m => m.Card.Id == "EDR_810t").ToArray();
        Core2026Proof.Check(leeches.Length == 1 && leeches.All(m => m.AttackDamage == 0 && m.Health == 2)
            && leeches[0].ZonePosition == 2, "exact Leech count/stats, appends after location");
        Core2026Proof.Check(enemy.Health == 3, "two targeted damage");
        StandardSetsProof.ParkBoard(p); LocationProofs.Location(p);
        while (p.BoardZone.FreeSpace > 1) Summon(p, "CS2_182");
        Play(g, p, id, enemy);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "EDR_810t") == 1, "one physical slot truncates summons");
        Play(g, p, id, enemy); Core2026Proof.Check(p.BoardZone.SlotCount == 7, "full board stays bounded");
        StandardSetsProof.ParkBoard(p); HideousHuskProof.ExerciseLeech(g, p);
        var scaled = DeathKnightC1Proof.NewGame(g.Seed); Summon(scaled.Player1, "EX1_012");
        var victim = Summon(scaled.Player2, "CS2_182"); Play(scaled, scaled.Player1, id, victim);
        Core2026Proof.Check(victim.Health == 2, "damage receives Spell Damage");
        var held = Add(scaled.Player1, id); var friendly = Summon(scaled.Player1, "CS2_182");
        var targets = scaled.Player1.Options().Where(o => o.Source?.Id == held.Id).Select(o => o.Target?.Id).ToArray();
        Core2026Proof.Check(targets.Contains(scaled.Player1.Hero.Id) && targets.Contains(scaled.Player2.Hero.Id)
            && targets.Contains(friendly.Id) && targets.Contains(victim.Id), "spell targets characters on either side");
    }
}
