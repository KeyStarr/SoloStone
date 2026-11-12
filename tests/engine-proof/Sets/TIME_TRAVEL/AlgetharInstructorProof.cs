using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AlgetharInstructorProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_856", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var victim = Summon(p.Opponent, "CS2_182");
        Play(g, p, "CS2_008", victim); Core2026Proof.Check(victim.Damage == 3, "Spell Damage +2 applied");
        source.Silence(); g.DeathProcessingAndAuraUpdate(); var second = Summon(p.Opponent, "CS2_182");
        Play(g, p, "CS2_008", second); Core2026Proof.Check(second.Damage == 1, "silence removes whole spell damage bonus");
    });
}
