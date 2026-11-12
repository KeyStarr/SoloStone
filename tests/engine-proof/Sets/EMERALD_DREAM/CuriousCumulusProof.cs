using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class CuriousCumulusProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_942", (g, p, id) => {
        var source = (Minion)Add(p, id);
        End(g); End(g);
        Core2026Proof.Check(!p.Hero.HasDivineShield, "hand Cumulus never triggers at turn end");
        PlayEntity(g, p, source);
        Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 5 && !p.Hero.HasDivineShield,
            "printed 3/5; play has no immediate shield");
        var clone = g.CloneExact(history: true); End(clone);
        Core2026Proof.Check(clone.Player1.Hero.HasDivineShield && !clone.Player2.Hero.HasDivineShield && !p.Hero.HasDivineShield,
            "cloned turn-end trigger grants shield without changing original");
        End(g);
        Core2026Proof.Check(p.Hero.HasDivineShield && !p.Opponent.Hero.HasDivineShield, "own end of turn shields only own hero");
        p.Hero.TakeDamage(p.Opponent.Hero, 8); p.Hero.TakeDamage(p.Opponent.Hero, 2);
        Core2026Proof.Check(!p.Hero.HasDivineShield && p.Hero.Damage == 2, "shield absorbs next damage then drops");
        End(g);
        Core2026Proof.Check(!p.Hero.HasDivineShield, "opponent turn end does not grant shield");
        End(g);
        Core2026Proof.Check(p.Hero.HasDivineShield, "trigger repeats at following friendly turn end");
        source.Silence();
        Core2026Proof.Check(p.Hero.HasDivineShield, "Silence cannot retract previously granted hero shield");
        p.Hero.TakeDamage(p.Opponent.Hero, 1); End(g); End(g);
        Core2026Proof.Check(!p.Hero.HasDivineShield, "Silence disables future end-turn grants");
        End(g);
        var removed = Summon(p, id); Kill(g, removed); End(g);
        Core2026Proof.Check(!p.Hero.HasDivineShield, "dead source cannot trigger");
        End(g);
        var enemy = Summon(p.Opponent, id); End(g);
        Core2026Proof.Check(!p.Hero.HasDivineShield && !p.Opponent.Hero.HasDivineShield, "opponent Cumulus ignores friendly turn end");
        End(g);
        Core2026Proof.Check(!p.Hero.HasDivineShield && p.Opponent.Hero.HasDivineShield, "opponent Cumulus shields its own controller");
        Kill(g, enemy);
        End(g);
        Core2026Proof.Check(p.Opponent.Hero.HasDivineShield, "granted shield persists across subsequent turn boundary and source death");
    });
}
