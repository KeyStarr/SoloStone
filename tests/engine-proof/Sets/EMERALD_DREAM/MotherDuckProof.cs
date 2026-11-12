using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MotherDuckProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_492", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play accepted");
        var ducks = p.BoardZone.Where(m => m.Card.Id == "EDR_492t").ToArray();
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id }.Concat(ducks.OrderBy(m => m.Id).Select(m => m.Id)).Append(right.Id)), "Ducklings summoned right of source in creation order between neighbours");
        Core2026Proof.Check(ducks.Length == 3 && ducks.All(m => m.AttackDamage == 1 && m.Health == 1 && m.IsRush && m.Card.Implemented), "three client Ducklings 1/1 Rush");
        var enemy = Summon(p.Opponent, "CS2_182");
        foreach (var duck in ducks) AttackOptionsProof.Lifecycle(g, duck, enemy, rush: true);
        foreach (bool removed in new[] { true, false })
        {
            ClearBoard(g, p);
            left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182");
            if (removed) Kill(g, source);
            else left[GameTag.DORMANT] = 1;
            source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
            var tokens = p.BoardZone.Where(m => m.Card.Id == "EDR_492t" && m.Id != source.Id).OrderBy(m => m.Id).ToArray();
            var expected = removed
                ? new[] { left.Id, right.Id }.Concat(tokens.Select(m => m.Id))
                : new[] { left.Id, source.Id }.Concat(tokens.Select(m => m.Id)).Append(right.Id);
            Core2026Proof.Check(tokens.Length == 3 && p.BoardZone.Select(m => m.Id).SequenceEqual(expected),
                removed ? "removed source appends tokens in creation order" : "Dormant left neighbour counts for source position");
            left[GameTag.DORMANT] = 0;
        }
        ClearBoard(g, p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "EDR_492t") == 1, "summons respect board space");
    });
}
