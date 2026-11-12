using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TarTyrantProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_605", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.AttackDamage == 1 && source.HasTaunt && source.HasLifeSteal, "own turn printed stats and keywords");
        var clone = g.CloneExact(history: true); End(clone); Core2026Proof.Check(((Minion)clone.IdEntityDic[source.Id]).AttackDamage == 7 && source.AttackDamage == 1, "turn aura updates only clone");
        End(g); Core2026Proof.Check(source.AttackDamage == 7, "opponent turn gains six Attack"); End(g); Core2026Proof.Check(source.AttackDamage == 1, "opponent turn buff expires");
        End(g); Play(g, p.Opponent, "BE_228", source); Core2026Proof.Check(source.AttackDamage == 1 && !source.HasTaunt && !source.HasLifeSteal, "Silence removes turn aura and keywords");
        End(g); ClearBoard(g, p); source = (Minion)Play(g, p, id); Kill(g, source); End(g); Core2026Proof.Check(source.AttackDamage == 1, "aura stops after leaving play");
    });
}
