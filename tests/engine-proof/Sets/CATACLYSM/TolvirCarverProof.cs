using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class TolvirCarverProof
{
    internal static void Run() => Unpinned_PayloadLifecycle();
    private static void Unpinned_PayloadLifecycle() => StandardSetsProof.Case("CATA_566", (g, p, id) => {
        var held = Add(p, "CS2_029"); Play(g, p, id);
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Contains(held.Id), "hand target choice");
        PlayerDecisionProof.Answer(g, held.Id);
        var same = Generic.Copy(p, p.Hero, held, Zone.HAND);
        var copy = Generic.Copy(p.Opponent, p.Opponent.Hero, held, Zone.HAND);
        StandardSetsProof.ClearBoard(g, p);
        End(g); Core2026Proof.Check(copy.Cost == 3 && held.Cost == 4, "Unpinned_OpposingCopy ticks at its controller start after Carver death");
        End(g); Core2026Proof.Check(held.Cost == 3 && same.Cost == 3 && copy.Cost == 3, "source and same-controller copy own start reduction");
        var restored = Game.LoadSnapshot(g.SaveSnapshot());
        End(restored); End(restored); Core2026Proof.Check(restored.IdEntityDic[held.Id].Cost == 2 && held.Cost == 3, "recurring trigger survives snapshot independently");
    });
}
