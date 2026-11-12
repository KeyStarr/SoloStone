using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ShadeoftheEndTimeProof
{
    internal static void Run() => StandardSetsProof.Case("END_031", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var spell = Add(p.Opponent, "CS2_029");
        End(g); p.Opponent.BaseMana = 10;
        Core2026Proof.Check(!p.Opponent.Options().Any(t => t.Source?.Id == spell.Id && t.Target?.Id == source.Id), "Stealth blocks enemy spell");
        End(g); var victim = Summon(p.Opponent, "CS2_182");
        Play(g, p, "CS2_008", victim); Core2026Proof.Check(victim.Damage == 2, "Spell Damage +1 applied");
        Attack(g, source, victim); Core2026Proof.Check(!source.HasStealth, "attack breaks Stealth");
    });
}
