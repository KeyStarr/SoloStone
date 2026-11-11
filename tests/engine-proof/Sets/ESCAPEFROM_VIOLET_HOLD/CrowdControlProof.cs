using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CrowdControlProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_307", (g, p, id) => {
        while (p.DeckZone.Count > 25) { var card = p.DeckZone[0]; p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        var spell = Add(p, id); Core2026Proof.Check(spell.Cost == 3 && spell[GameTag.COST] == 3, "25-card deck boundary discount visible");
        var clone = g.CloneExact(history: true); var removed = clone.Player1.DeckZone[0]; clone.Player1.DeckZone.Remove(removed); clone.Player1.SetasideZone.Add(removed); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(clone.IdEntityDic[spell.Id][GameTag.COST] == 5 && spell.Cost == 3, "24-card boundary, clone isolated");
        var egg = Summon(p, "FP1_002"); var sturdy = Summon(p.Opponent, "CS2_182"); var shield = Summon(p, "CS2_182"); shield.HasDivineShield = true;
        PlayEntity(g, p, spell);
        Core2026Proof.Check(egg.Zone.Type == Zone.GRAVEYARD && p.BoardZone.Count == 1 && p.BoardZone[0].Id == shield.Id, "deaths resolve between waves, Creepers die to second wave");
        Core2026Proof.Check(sturdy.Damage == 4 && shield.Damage == 2 && !shield.HasDivineShield, "two waves hit both sides and consume shield once");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); Summon(p, "EX1_012"); sturdy = Summon(p.Opponent, "CS2_200");
        Play(g, p, id); Core2026Proof.Check(sturdy.Damage == 5, "each wave recalculates spell power after Thalnos dies");
    });
}
