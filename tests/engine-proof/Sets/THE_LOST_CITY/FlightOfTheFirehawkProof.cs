using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FlightOfTheFirehawkProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_222", (g, p, id) => {
        foreach (var item in p.DeckZone.ToArray()) { p.DeckZone.Remove(item); p.SetasideZone.Add(item); }
        var beast = (Minion)Entity.FromCard(p, Cards.FromId("CS2_171"), zone: p.DeckZone);
        var elemental = (Minion)Entity.FromCard(p, Cards.FromId("CS2_033"), zone: p.DeckZone);
        var spell = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        Play(g, p, id);
        Core2026Proof.Check(beast.Zone == p.HandZone && elemental.Zone == p.HandZone && beast.AttackDamage == 2 && beast.BaseHealth == 2 && elemental.AttackDamage == 4 && elemental.BaseHealth == 7,
            "draws two different types and buffs both by +1/+1");
        Core2026Proof.Check(spell.Zone == p.DeckZone, "spell never drawn");
        var dual = (Minion)Entity.FromCard(p, Cards.FromId("DINO_413"), zone: p.DeckZone);
        var shared = (Minion)Entity.FromCard(p, Cards.FromId("CS2_171"), zone: p.DeckZone);
        Play(g, p, id);
        Core2026Proof.Check(new[] { dual, shared }.Count(m => m.Zone == p.HandZone) == 1 && p.DeckZone.Count == 2, "overlapping secondary types cannot fill both draws");
        foreach (var item in p.DeckZone.ToArray()) { p.DeckZone.Remove(item); p.SetasideZone.Add(item); }
        var typeless = (Minion)Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        Play(g, p, id); Core2026Proof.Check(typeless.Zone == p.DeckZone, "typeless minion has no minion type to draw");
        var burned = (Minion)Entity.FromCard(p, Cards.FromId("CS2_171"), zone: p.DeckZone);
        var cast = Entity.FromCard(p, Cards.FromId(id)); while (p.HandZone.Count < 10) Add(p, "CS2_182");
        cast.Card.Power.PowerTask.Process(g, p, cast, null, new TaskStack());
        Core2026Proof.Check(burned.Zone == p.GraveyardZone && burned.AttackDamage == 1 && burned.BaseHealth == 1, "burned draw receives no buff");
    });
}
