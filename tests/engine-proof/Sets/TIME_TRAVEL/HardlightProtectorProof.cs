using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class HardlightProtectorProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_015", (g, p, id) => {
        p.Hero.Damage = 8; p.Opponent.Hero.Damage = 6;
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 1 && source.HasDivineShield,
            "printed 2/1 Divine Shield");
        Core2026Proof.Check(p.Hero.Damage == 5 && p.Hero.HasDivineShield && p.Opponent.Hero.Damage == 6
            && !p.Opponent.Hero.HasDivineShield, "heal three then shield only friendly hero");
        p.Hero.TakeDamage(p.Opponent.Hero, 0);
        Core2026Proof.Check(p.Hero.HasDivineShield, "zero damage retains granted hero shield");
        var clone = g.CloneExact(history: true);
        clone.Player1.Hero.TakeDamage(clone.Player2.Hero, 9);
        Core2026Proof.Check(!clone.Player1.Hero.HasDivineShield && clone.Player1.Hero.Damage == 5
            && p.Hero.HasDivineShield, "cloned card-granted shield absorbs damage independently");
        source.Silence(); Kill(g, source); End(g); End(g);
        Core2026Proof.Check(p.Hero.HasDivineShield, "hero shield persists after source Silence, death and turn boundary");
        p.Hero.Armor = 4;
        p.Hero.TakeDamage(p.Opponent.Hero, 9);
        Core2026Proof.Check(!p.Hero.HasDivineShield && p.Hero.Damage == 5 && p.Hero.Armor == 4,
            "shield absorbs entire next hit before armor");
        p.Hero.TakeDamage(p.Opponent.Hero, 6);
        Core2026Proof.Check(p.Hero.Damage == 7 && p.Hero.Armor == 0, "following hit uses armor and Health normally");
        p.Hero.Damage = 1;
        Play(g, p, id); Play(g, p, id);
        Core2026Proof.Check(p.Hero.Damage == 0 && p.Hero.HasDivineShield, "healing caps at full Health; repeated grant accepted");
        p.Hero.TakeDamage(p.Opponent.Hero, 1); p.Hero.TakeDamage(p.Opponent.Hero, 1);
        Core2026Proof.Check(!p.Hero.HasDivineShield && p.Hero.Damage == 1, "repeated grants never stack shields");
    });
}
