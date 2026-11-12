using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class StranglevineProof
{
    internal static void Run()
    {
        Unpinned_Stranglevine();
        BonusCopyRegressionProof.Unpinned_StranglevineDormant();
    }
    private static void Unpinned_Stranglevine() => StandardSetsProof.Case("TLC_465", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var recipient = Summon(p, "CS2_182");
        End(g); Kill(g, source);
        Core2026Proof.Check(BonusEffectsProof.Count(recipient) == 1 && !recipient.IsRush && recipient.HasDeathrattle
            && recipient.AppliedEnchantments.Any(x => x.Card.Id == "TLC_465e"), "opponent-turn death transfers keyword excluding Rush then Deathrattle");
        recipient[GameTag.REBORN] = 0;
        var next = Summon(p, "CS2_182"); Kill(g, recipient);
        Core2026Proof.Check(BonusEffectsProof.Count(next) == 1 && next.HasDeathrattle, "inherited Deathrattle transfers again");
        next.Silence(); var control = Summon(p, "CS2_182"); Kill(g, next);
        Core2026Proof.Check(BonusEffectsProof.Count(control) == 0 && !control.HasDeathrattle, "Silence stops propagation");
        Kill(g, control); var alone = Summon(p, id); Kill(g, alone);
        Core2026Proof.Check(p.BoardZone.Count == 0, "empty board stops cleanly");
    });
}
