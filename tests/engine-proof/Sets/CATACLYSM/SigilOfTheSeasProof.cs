using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SigilOfTheSeasProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_528", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 0, "no immediate summon");
        var clone = g.CloneExact(history: true); End(clone); End(clone);
        Core2026Proof.Check(clone.Player1.BoardZone.Single().Card.Id == "CATA_528t" && p.BoardZone.Count == 0, "clone delayed trigger isolated");
        End(g); Core2026Proof.Check(p.BoardZone.Count == 0, "opponent start does not trigger");
        End(g); var naga = p.BoardZone.Single();
        Core2026Proof.Check(naga.Card.Id == "CATA_528t" && naga.Card.Implemented && naga.AttackDamage == 3 && naga.Health == 3 && naga.HasTaunt && naga.Card.IsRace((Race)92), "exact 3/3 Taunt Naga");
        End(g); End(g); Core2026Proof.Check(p.BoardZone.Count == 1, "one shot only");
        ClearBoard(g, p); for (int i = 0; i < 7; i++) Summon(p, "CS2_182"); var blocked = Play(g, p, id);
        Core2026Proof.Check(SigilRules.IsActive(blocked), "pending Sigil waits in SECRET"); End(g); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.All(m => m.Card.Id != "CATA_528t"), "full board loses summon");
        Core2026Proof.Check(blocked.Zone.Type == Zone.GRAVEYARD && !SigilRules.IsActive(blocked), "Sigil with no room still leaves SECRET");
        Kill(g, p.BoardZone[0]); End(g); End(g); Core2026Proof.Check(p.BoardZone.Count == 6, "blocked summon does not retry");
    });
}
