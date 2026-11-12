using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class VioletSpellwingProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_DRG_107", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 1 && source.IsRace(Race.ELEMENTAL) && source.IsRace(Race.BEAST), "printed 2/1 with both races");
        Kill(g, source);
        var missiles = p.HandZone.Single();
        Core2026Proof.Check(missiles.Card.Id == "EX1_277" && missiles.Cost == 1 && missiles.Card.Implemented, "exact referenced Arcane Missiles");
        PlayEntity(g, p, missiles);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 3, "generated Arcane Missiles is playable and deals three");
        source = (Minion)Play(g, p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 0, "Silence removes Deathrattle");
        source = Summon(p, id); while (p.HandZone.Count < 10) Add(p, "CS2_182");
        Kill(g, source); Core2026Proof.Check(p.HandZone.Count == 10 && !p.HandZone.Any(c => c.Card.Id == "EX1_277"), "full hand burns generated spell");
    });
}
