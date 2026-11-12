using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ShadowsOfYesterdayProof
{
    internal static void Run() => Unpinned_ShadowsOfYesterday();
    private static void Unpinned_ShadowsOfYesterday() => StandardSetsProof.Case("TIME_610", (g, p, id) => {
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 4 && p.BoardZone.All(x => x.Card.Id == "TIME_610t2" && x.AttackDamage == 3 && x.BaseHealth == 2 && BonusEffectsProof.Count(x) == 2), "four exact Shades with two distinct grants each");
        Core2026Proof.Check(g.Rewind != null, "Rewind transaction available");
        var firstIds = p.BoardZone.Select(x => x.Id).ToArray();
        Core2026Proof.Check(g.Process(ChooseTask.Pick(p, g.Rewind.RewindEntityId)), "rewind Shades accepted");
        p = g.Player1;
        Core2026Proof.Check(p.BoardZone.Count == 4 && p.BoardZone.All(x => !firstIds.Contains(x.Id) && BonusEffectsProof.Count(x) == 2), "Rewind removes old summons and grants before retry");
        if (g.Rewind != null) g.Process(ChooseTask.Pick(p, g.Rewind.KeepEntityId));
        ParkBoard(p); for (int i=0;i<6;i++) Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Last().Card.Id == "TIME_610t2" && BonusEffectsProof.Count(p.BoardZone.Last()) == 2, "only successful short-board Shade receives grants");
        if (g.Rewind != null) g.Process(ChooseTask.Pick(p, g.Rewind.KeepEntityId));
    });
}
