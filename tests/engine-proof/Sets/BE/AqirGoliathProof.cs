using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AqirGoliathProof
{
    internal static void Run() => StandardSetsProof.Case("BE_721", (g, p, id) => {
        var source = Add(p, id); Core2026Proof.Check(source.Cost == 8, "base cost");
        Play(g, p, "CS2_182"); Core2026Proof.Check(source.Cost == 8, "noncombo excluded");
        Play(g, p, "EX1_134", p.Opponent.Hero); Core2026Proof.Check(source.Cost == 6, "printed Combo minion counted");
        Play(g, p, "EX1_124", p.Opponent.Hero); Core2026Proof.Check(source.Cost == 4, "printed Combo spell counted");
        var clone = g.Clone(); Play(clone, clone.Player1, "EX1_124", clone.Player2.Hero);
        Core2026Proof.Check(clone.IdEntityDic[source.Id].Cost == 2 && source.Cost == 4, "cost fact clone isolated");
        End(g); Play(g, p.Opponent, "EX1_124", p.Hero); Core2026Proof.Check(source.Cost == 8, "own turn facts reset and opponent Combo excluded"); End(g);
        for (int i = 0; i < 5; i++) Play(g, p, "EX1_124", p.Opponent.Hero);
        Core2026Proof.Check(source.Cost == 0, "cost floors at zero");
        int deck = p.DeckZone.Count; PlayEntity(g, p, source);
        Core2026Proof.Check(source is Minion m && m.AttackDamage == 4 && m.Health == 5 && p.DeckZone.Count == deck - 1, "zero-cost Goliath draws one");
        Play(g, p, "BE_228", (Minion)source); Core2026Proof.Check(source.ActivatedTrigger == null && source.OngoingEffect == null, "silence no ongoing cost effect on board");
    });
}
