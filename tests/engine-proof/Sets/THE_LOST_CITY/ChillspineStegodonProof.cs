using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ChillspineStegodonProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_413", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.ELEMENTAL) && source.IsRace(Race.BEAST) && enemy.Damage == 2 && !enemy.IsFrozen, "dual types, single enemy hit once without Kindred");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        Play(g, p, "CS2_033"); End(g); End(g);
        var targets = Enumerable.Range(0, 4).Select(_ => Summon(p.Opponent, "CS2_182")).ToArray();
        Play(g, p, id);
        Core2026Proof.Check(targets.Count(m => m.Damage == 2 && m.IsFrozen) == 2 && targets.Count(m => m.Damage == 0 && !m.IsFrozen) == 2, "previous-turn Elemental activates exactly two distinct random targets");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); End(g); End(g); End(g); Play(g, p.Opponent, "CS2_171"); End(g);
        enemy = Summon(p.Opponent, "CS2_182"); Play(g, p, "CS2_171"); Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 2 && !enemy.IsFrozen, "opponent previous-turn Beast and own current-turn Beast do not activate");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 0, "empty enemy board does not hit hero");
    });
}
