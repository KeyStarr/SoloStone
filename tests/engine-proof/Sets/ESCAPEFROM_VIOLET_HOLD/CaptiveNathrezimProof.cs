using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CaptiveNathrezimProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_890", (g, p, id) => {

        var held = Add(p, "CS2_182"); var enemy = Add(p.Opponent, "CS2_182"); var spell = Add(p, "CS2_029");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasTaunt && held.Cost == 6 && held[GameTag.COST] == 6 && enemy.Cost == 6 && spell.Cost == 4, "both hands taxed, spells excluded");
        var fresh = Add(p, "CS2_120"); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(fresh.Cost == 4, "new hand entrants taxed");
        var clone = g.CloneExact(); ((Minion)clone.IdEntityDic[source.Id]).Silence(); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(clone.IdEntityDic[held.Id].Cost == 4 && held.Cost == 6, "clone aura independent");
        End(g); End(g); Core2026Proof.Check(held.Cost == 6, "aura persists across turns");
        source.Silence(); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(held.Cost == 4 && enemy.Cost == 4, "Silence removes both taxes");
        Kill(g, source); source = Summon(p, id); Core2026Proof.Check(held.Cost == 6, "summoned source activates");
        Kill(g, source); Core2026Proof.Check(held.Cost == 4 && enemy.Cost == 4, "death removes aura");
    });
}
