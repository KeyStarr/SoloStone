using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TorgaProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_102", (g, p, id) => {
        // Lean_SpellSchoolKindred: kindred-school-match.yaml / kindred-turn-window.yaml.
        var frost = Add(p, "TLC_440");
        Play(g, p, "TLC_440", p.Opponent.Hero);
        Core2026Proof.Check(!SabberStoneCore.Conditions.SelfCondition.Kindred.Eval(frost), "Lean_: current-turn school does not match");
        End(g); End(g);
        Core2026Proof.Check(SabberStoneCore.Conditions.SelfCondition.Kindred.Eval(frost), "Lean_: previous-own-turn Frost activates spell Kindred");
        p.SetasideZone.Add(p.HandZone.Remove(frost));
        foreach (var item in p.DeckZone.ToArray()) { p.DeckZone.Remove(item); p.SetasideZone.Add(item); }
        var kindred = Entity.FromCard(p, Cards.FromId("DINO_413"), zone: p.DeckZone);
        var activator = Entity.FromCard(p, Cards.FromId("CS2_033"), zone: p.DeckZone);
        var unrelated = Entity.FromCard(p, Cards.FromId("EX1_506"), zone: p.DeckZone);
        var spell = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.UNDEAD) && source.IsRace(Race.BEAST) && kindred.Zone == p.HandZone && activator.Zone == p.HandZone,
            "draws Kindred minion plus activator matching its secondary Elemental type");
        Core2026Proof.Check(unrelated.Zone == p.DeckZone && spell.Zone == p.DeckZone, "unrelated types and spells stay in deck");
        ClearBoard(g, p); Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count == 2, "no Kindred card means neither draw");
        ClearBoard(g, p); kindred = Entity.FromCard(p, Cards.FromId("DINO_413"), zone: p.DeckZone); Play(g, p, id);
        Core2026Proof.Check(kindred.Zone == p.HandZone && p.DeckZone.Count == 2, "no activator still draws Kindred card");
        ClearBoard(g, p);
        kindred = Entity.FromCard(p, Cards.FromId("DINO_413"), zone: p.DeckZone);
        activator = Entity.FromCard(p, Cards.FromId("CS2_033"), zone: p.DeckZone);
        var cast = Entity.FromCard(p, Cards.FromId(id)); while (p.HandZone.Count < 10) Add(p, "CS2_182");
        cast.Card.Power.PowerTask.Process(g, p, cast, null, new TaskStack());
        Core2026Proof.Check(kindred.Zone == p.GraveyardZone && activator.Zone == p.GraveyardZone, "full hand burns both tutored cards");
        // Lean_TorgaSchoolActivator: kindred-torga.yaml.
        foreach (var item in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(item));
        kindred = Entity.FromCard(p, Cards.FromId("TLC_816"), zone: p.DeckZone);
        activator = Entity.FromCard(p, Cards.FromId("CORE_CS2_004"), zone: p.DeckZone);
        cast.Card.Power.PowerTask.Process(g, p, cast, null, new TaskStack());
        Core2026Proof.Check(kindred.Zone == p.HandZone && activator.Zone == p.HandZone && spell.Zone == p.DeckZone,
            "Lean_: Kindred spell draws same-school activator, not unrelated Fire");
        kindred = Entity.FromCard(p, Cards.FromId("TLC_816"), zone: p.DeckZone);
        cast.Card.Power.PowerTask.Process(g, p, cast, null, new TaskStack());
        Core2026Proof.Check(kindred.Zone == p.HandZone && p.DeckZone.Count == 2, "Lean_: absent same-school activator still draws spell");
        kindred = Entity.FromCard(p, Cards.FromId("TLC_816"), zone: p.DeckZone);
        activator = Entity.FromCard(p, Cards.FromId("CORE_CS2_004"), zone: p.DeckZone);
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        cast.Card.Power.PowerTask.Process(g, p, cast, null, new TaskStack());
        Core2026Proof.Check(kindred.Zone == p.GraveyardZone && activator.Zone == p.GraveyardZone, "Lean_: full hand burns spell and its school activator");
    });
}
