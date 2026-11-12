using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CuriousExplorerProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_244", (g, p, id) => {
        var own = Add(p, "CS2_182"); var spell = Add(p.Opponent, "CS2_029"); var target = Add(p.Opponent, "CS2_182");
        var source = (Minion)Play(g, p, id); var clone = g.CloneExact(history: true);
        Kill(clone, (Minion)clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(clone.IdEntityDic[target.Id].Cost == 2 && target.Cost == 4, "clone discounts only clone opponent minion");
        Kill(g, source); Core2026Proof.Check(target.Cost == 2 && target[GameTag.COST] == 2 && own.Cost == 4 && spell.Cost == 4, "only opponent minion gets visible cost reduction");
        var copiedGame = g.CloneExact(history: true); End(copiedGame);
        var existingHand = copiedGame.Player2.HandZone.Select(m => m.Id).ToHashSet();
        Play(copiedGame, copiedGame.Player2, "ICC_823");
        var copies = copiedGame.Player2.HandZone.Where(m => m.Id == target.Id || !existingHand.Contains(m.Id)).ToArray();
        Core2026Proof.Check(copies.Length == 2 && copies.All(m => m.Cost == 2 && m[GameTag.COST] == 2)
            && target.Cost == 2 && p.Opponent.HandZone.Count == 2, "Simulacrum retains permanent discount without changing original game");
        source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source);
        Core2026Proof.Check(target.Cost == 2, "Silence prevents discount of eligible positive-cost minion");
        source = (Minion)Play(g, p, id); Kill(g, source); Core2026Proof.Check(target.Cost == 0, "repeated discount floors at zero");
        End(g); Core2026Proof.Check(target.Cost == 0, "discount persists across turns");
        PlayEntity(g, p.Opponent, target); Core2026Proof.Check(p.Opponent.BoardZone.Single().Card.Id == "CS2_182", "discounted minion legally playable");
        End(g); source = (Minion)Play(g, p, id); Kill(g, source); Core2026Proof.Check(spell.Cost == 4, "no opponent minion means no discount");
    });
}
