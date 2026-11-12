using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class WickedBlightspawnProof
{
    internal static void Run() => StandardSetsProof.Case("END_002", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play accepted");
        var right = Summon(p, "CS2_182");
        Core2026Proof.Check(source[GameTag.REBORN] == 1 && source.IsRace(Race.UNDEAD), "printed Reborn and Undead");
        Play(g, p, "CS2_029", source);
        var reborn = p.BoardZone.Single(m => m.Card.Id == id);
        Core2026Proof.Check(p.Hero.Weapon.Card.Id == "CS2_082" && p.Hero.Weapon.AttackDamage == 1 && p.Hero.Weapon.Durability == 2, "death equips exact 1/2 dagger");
        Core2026Proof.Check(reborn.Health == 1 && reborn.AttackDamage == 4 && reborn[GameTag.REBORN] == 0 && reborn.ZonePosition == 1 && p.BoardZone[0] == left && p.BoardZone[2] == right, "Reborn returns between neighbours with one health");
        var weapon = p.Hero.Weapon; Play(g, p, "CS2_029", reborn);
        Core2026Proof.Check(p.Hero.Weapon == weapon && weapon.AttackDamage == 3 && weapon.Durability == 2 && weapon.AppliedEnchantments.Single().Card.Id == "END_002e", "existing weapon gains two attack, no replacement");
        Core2026Proof.Check(!p.BoardZone.Any(m => m.Card.Id == id), "Reborn only once");
        source = (Minion)Play(g, p, id); source.Silence(); Play(g, p, "CS2_029", source);
        Core2026Proof.Check(weapon.AttackDamage == 3 && !p.BoardZone.Any(m => m.Card.Id == id), "Silence removes Reborn and Deathrattle");
        source = (Minion)Play(g, p, id); while (p.BoardZone.Count < 7) Summon(p, "CS2_182"); Play(g, p, "CS2_029", source);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Single(m => m.Card.Id == id).Health == 1, "Reborn respects seven-slot board");
    });
}
