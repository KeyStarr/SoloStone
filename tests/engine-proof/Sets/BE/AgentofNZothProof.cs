using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AgentofNZothProof
{
    internal static void Run() => StandardSetsProof.Case("BE_541", (g, p, id) => {
        var same = Add(p, id); var plain = Add(p, "CS2_182"); var spell = Add(p, "CS2_029");
        var source = Summon(p, id); int before = p.HandZone.Count; Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == before, "no different Deathrattle candidate");
        var candidate = (Minion)Add(p, "BE_572"); candidate.AttackDamage = 8; candidate.BaseHealth = 9;
        source = Summon(p, id); Kill(g, source);
        var copy = p.HandZone.Single(m => m.Card.Id == "BE_572" && m.Id != candidate.Id);
        Core2026Proof.Check(copy is Minion cm && cm.AttackDamage == 8 && cm.Health == 9 && cm.HasTaunt && cm.HasDeathrattle && p.HandZone.Contains(candidate), "different hand Deathrattle copied with buffs, original retained");
        var second = Add(p, "BE_859"); source = Summon(p, id); before = p.HandZone.Count; Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == before + 1 && p.HandZone.Last().Card.Id is "BE_572" or "BE_859", "random choice restricted to eligible hand minions");
        source = Summon(p, id); Play(g, p, "BE_228", source); before = p.HandZone.Count; Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == before, "silence suppresses Deathrattle");
        while (!p.HandZone.IsFull) Add(p, "CS2_182"); source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Contains(same) && p.HandZone.Contains(plain) && p.HandZone.Contains(spell), "full hand respects limit without consuming cards");
    });
}
