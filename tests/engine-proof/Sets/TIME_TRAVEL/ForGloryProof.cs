using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ForGloryProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_715", (g, p, id) => {
        var source = Add(p, id); Core2026Proof.Check(source.Cost == 5, "empty opponent board full cost");
        Summon(p, "CS2_182"); Core2026Proof.Check(source.Cost == 5, "friendly minion gives no discount");
        var enemy = Summon(p.Opponent, "CS2_182"); Core2026Proof.Check(source.Cost == 4, "one enemy discounts one");
        var clone = g.CloneExact(history: true); Summon(clone.Player2, "CS2_182");
        Core2026Proof.Check(clone.IdEntityDic[source.Id].Cost == 3 && source.Cost == 4, "adaptive hand cost clone isolated");
        for (int i = 1; i < 7; i++) Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(source.Cost == 0, "cost floors at zero on full board");
        ClearBoard(g, p.Opponent); Core2026Proof.Check(source.Cost == 5, "leaving play restores full cost immediately");
        int deck = p.DeckZone.Count; PlayEntity(g, p, source);
        Core2026Proof.Check(p.DeckZone.Count == deck - 2 && ((Playable)source).OngoingEffect == null, "draws two and removes hand cost effect on play");
    });
}
