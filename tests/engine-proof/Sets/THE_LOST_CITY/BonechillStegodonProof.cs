using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BonechillStegodonProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_401", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.UNDEAD) && source.IsRace(Race.BEAST), "printed dual types");
        var targets = Enumerable.Range(0, 4).Select(_ => Summon(p.Opponent, "CS2_186")).ToArray();
        Kill(g, source);
        Core2026Proof.Check(targets.Sum(m => m.Damage) + p.Opponent.Hero.Damage == 18
            && targets.Count(m => m.Damage == 6) + (p.Opponent.Hero.Damage == 6 ? 1 : 0) == 3, "three distinct enemies take six each");
        ClearBoard(g, p.Opponent); p.Opponent.Hero.Damage = 0;
        source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 6, "only hero takes one hit");
        source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 6, "Silence removes Deathrattle");
    });
}
