using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LivingParadoxProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_059", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play accepted");
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == id) == 3 && p.BoardZone.Where(m => m.Card.Id == id).All(m => m.Card.Id == id && m.AttackDamage == 2 && m.Health == 1 && m.CantBeTargetedBySpells), "two 2/1 Elusive copies; no recursive battlecry");
        var copies = p.BoardZone.Where(m => m.Card.Id == id && m.Id != source.Id).OrderBy(m => m.Id);
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id }.Concat(copies.Select(m => m.Id)).Append(right.Id)), "Paradoxes summoned right of source in creation order between neighbours");
        Elusive(g, p, source);
        foreach (bool removed in new[] { true, false })
        {
            ClearBoard(g, p);
            left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182");
            if (removed) Kill(g, source);
            else left[GameTag.DORMANT] = 1;
            source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
            var tokens = p.BoardZone.Where(m => m.Card.Id == "TIME_059" && m.Id != source.Id).OrderBy(m => m.Id).ToArray();
            var expected = removed
                ? new[] { left.Id, right.Id }.Concat(tokens.Select(m => m.Id))
                : new[] { left.Id, source.Id }.Concat(tokens.Select(m => m.Id)).Append(right.Id);
            Core2026Proof.Check(tokens.Length == 2 && p.BoardZone.Select(m => m.Id).SequenceEqual(expected),
                removed ? "removed source appends tokens in creation order" : "Dormant left neighbour counts for source position");
            left[GameTag.DORMANT] = 0;
        }
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 1, "full board suppresses both summons");
    });
}
