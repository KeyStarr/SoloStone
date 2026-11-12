using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CritterCaretakerProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_971", (g, p, id) => {
        var source = (Minion)Play(g, p, id); p.Hero.Damage = 8; p.Opponent.Hero.Damage = 2;
        var minion = Summon(p, "CS2_182"); minion.Damage = 2;
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(clone.Player1.Hero.Damage == 5 && p.Hero.Damage == 8, "healing clone isolated");
        End(g); Core2026Proof.Check(p.Hero.Damage == 5 && p.Opponent.Hero.Damage == 0 && minion.Damage == 2, "restore three to both heroes capped at maximum; no minion healing");
        End(g); Core2026Proof.Check(p.Hero.Damage == 5, "opponent turn excluded");
        Play(g, p, "BE_228", source); End(g); End(g);
        Core2026Proof.Check(p.Hero.Damage == 5 && source.ActivatedTrigger == null, "Silence stops healing");
        Kill(g, source); source = (Minion)Play(g, p, id); Kill(g, source); End(g); End(g);
        Core2026Proof.Check(p.Hero.Damage == 5, "dead source stops healing");
        source = (Minion)Play(g, p, id); p.Hero.Damage = 0; End(g); End(g);
        Core2026Proof.Check(p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "healthy heroes unchanged");
    });
}
