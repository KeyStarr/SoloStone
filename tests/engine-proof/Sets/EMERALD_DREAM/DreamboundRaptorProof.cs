using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DreamboundRaptorProof
{
    internal static void Run()
    {
        Unpinned_DreamboundRaptor();
        BonusCopyRegressionProof.Unpinned_RaptorDormant();
    }
    private static void Unpinned_DreamboundRaptor() => StandardSetsProof.Case("EDR_849", (g, p, id) => {
        var raptor = (Minion)Play(g, p, id);
        Core2026Proof.Check(BonusEffectsProof.Count(raptor) == 0, "Raptor does not trigger its own play");
        var played = (Minion)Play(g, p, "CS2_182");
        Core2026Proof.Check(BonusEffectsProof.Count(played) == 1, "played minion gains one effect");
        var summoned = Summon(p, "CS2_182"); Core2026Proof.Check(BonusEffectsProof.Count(summoned) == 0, "summon excluded");
        End(g); var enemy = (Minion)Play(g, p.Opponent, "CS2_182");
        Core2026Proof.Check(BonusEffectsProof.Count(enemy) == 0, "enemy play excluded"); End(g);
        raptor.Silence(); var noGrant = (Minion)Play(g, p, "CS2_182");
        Core2026Proof.Check(BonusEffectsProof.Count(noGrant) == 0, "Silence removes trigger");
    });
}
