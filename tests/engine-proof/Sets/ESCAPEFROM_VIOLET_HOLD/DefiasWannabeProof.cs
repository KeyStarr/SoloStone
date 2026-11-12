using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DefiasWannabeProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_909", (g, p, id) => {

        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 2, "no prior card no combo buff");
        var held = Add(p, id); Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "draw initializes live combo number");
        Play(g, p, "CS2_120"); Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "held combo number updates");
        PlayEntity(g, p, held); Core2026Proof.Check(((Minion)held).AttackDamage == 4 && ((Minion)held).Health == 4, "two other cards give +2/+2");
        var clone = g.CloneExact(); ((Minion)clone.IdEntityDic[held.Id]).Silence();
        Core2026Proof.Check(((Minion)held).AttackDamage == 4, "clone independent");
        ((Minion)held).Silence(); Core2026Proof.Check(((Minion)held).AttackDamage == 2 && ((Minion)held).Health == 2, "Silence removes combo buff");
        held = Add(p, id); End(g); End(g); Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0, "new turn resets held combo number");
    });
}
