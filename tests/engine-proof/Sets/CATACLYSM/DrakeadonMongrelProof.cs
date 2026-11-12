using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class DrakeadonMongrelProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_723", (g, p, id) => {
        SourceExcluded(g, id);
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182"); var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle Mongrel play accepted");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, right.Id }), "middle placement without Battlecry generation");
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        var generated = p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == source.Id).ToArray();
        Generated(g, source, generated, 2);
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id }.Concat(generated.Select(m => m.Id)).Append(right.Id)), "deathrattle summons replace source in creation order between surviving neighbors");
        foreach (var minion in p.BoardZone.ToArray()) minion.Silence();
        ClearBoard(g, p);
        source = Summon(p, id);
        Pool(g, id, 0, () => {
            var random = g.Random.CaptureState(); Kill(g, source);
            Core2026Proof.Check(p.BoardZone.Count == 0 && g.Random.CaptureState().Equals(random), "empty deathrattle still removes source, no RNG");
        });
        Pool(g, id, 1, () => {
            source = Summon(p, id); Kill(g, source);
            Generated(g, source, p.BoardZone, 2);
            Core2026Proof.Check(p.BoardZone.Select(m => m.Card.Id).Distinct().Count() == 1, "independent summons can duplicate");
        });
        foreach (var minion in p.BoardZone.ToArray()) minion.Silence();
        ClearBoard(g, p);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        source = Summon(p, id); var before = g.Random.DrawCount; Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 7 && g.Random.DrawCount - before == 2, "one board slot still samples twice");
        before = g.Random.DrawCount; Resolve(g, source, death: true);
        Core2026Proof.Check(p.BoardZone.Count == 7 && g.Random.DrawCount - before == 2, "full board still samples twice");
        foreach (var minion in p.BoardZone.ToArray()) minion.Silence();
        ClearBoard(g, p);
        source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 0, "Silence removes random-summon deathrattle");
    });
}
