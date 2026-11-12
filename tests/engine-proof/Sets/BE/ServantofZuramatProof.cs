using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ServantofZuramatProof
{
    internal static void Run() => StandardSetsProof.Case("BE_228", (g, p, id) => {
        var empty = (Minion)Play(g, p, id); Core2026Proof.Check(!empty.IsSilenced, "optional target: play on empty board");
        var enemy = Summon(p.Opponent, "CS2_179"); Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.IsSilenced && !enemy.HasTaunt, "silences enemy minion");
        var friendly = Summon(p, "CS2_179"); Play(g, p, id, friendly);
        Core2026Proof.Check(friendly.IsSilenced && !friendly.HasTaunt, "silences friendly minion");
        TargetRules(g, p, id, enemy, friendly, false);
    });
}
