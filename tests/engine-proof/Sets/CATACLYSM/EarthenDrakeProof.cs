using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EarthenDrakeProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_999", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.ELEMENTAL) && source.IsRace(Race.DRAGON) && source.AttackDamage == 4 && source.Health == 4, "printed 4/4 Elemental Dragon");
        var clone = g.CloneExact(history: true); End(clone);
        Core2026Proof.Check(clone.Player2.Hero.Damage == 4 && p.Opponent.Hero.Damage == 0, "end-turn trigger affects clone alone");
        End(g); Core2026Proof.Check(p.Opponent.Hero.Damage == 4 && p.Hero.Damage == 0, "own end turn deals four to enemy hero");
        End(g); Core2026Proof.Check(p.Opponent.Hero.Damage == 4, "opponent end turn does not trigger");
        source.Silence(); g.DeathProcessingAndAuraUpdate(); End(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 4 && source.IsRace(Race.ELEMENTAL) && source.IsRace(Race.DRAGON), "Silence removes trigger, retains both types");
        End(g); source = (Minion)Play(g, p, id); Kill(g, source); End(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 4, "leaving play removes trigger");
    });
}
