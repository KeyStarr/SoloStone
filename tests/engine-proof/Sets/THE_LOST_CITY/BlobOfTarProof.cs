using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BlobOfTarProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_468", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var source = (Minion)Play(g, p, id); var right = Summon(p, "CS2_182");
        Core2026Proof.Check(source.HasTaunt && source.Poisonous && source.IsRace(Race.ELEMENTAL), "printed Poisonous Taunt Elemental");
        Kill(g, source); var poison = p.BoardZone.Single(m => m.Card.Id == "TLC_468t1"); var taunt = p.BoardZone.Single(m => m.Card.Id == "TLC_468t2");
        Core2026Proof.Check(poison.AttackDamage == 2 && poison.Health == 2 && poison.Poisonous && !poison.HasTaunt && taunt.AttackDamage == 2 && taunt.Health == 2 && taunt.HasTaunt && !taunt.Poisonous, "two pinned 2/2 Blobs with distinct keywords");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { poison.Id, taunt.Id, left.Id, right.Id }), "dead leftmost source's slot takes Blobs in creation order");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source); Core2026Proof.Check(p.BoardZone.Count == 1, "silence removes deathrattle");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        source = (Minion)Play(g, p, id); Kill(g, source); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone[0].Card.Id == "TLC_468t1", "one space summons Poisonous Blob first into the freed slot");
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182");
        left[GameTag.DORMANT] = 1;
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        var generated = p.BoardZone.Where(m => new[] { "TLC_468t1", "TLC_468t2" }.Contains(m.Card.Id)).OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(generated.Length == 2 && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id }.Concat(generated.Select(m => m.Id)).Append(right.Id)), "live source summons right in creation order with Dormant left neighbour");
        left[GameTag.DORMANT] = 0;
        while (p.BoardZone.Count < 7) Summon(p, "CS2_182");
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => new[] { "TLC_468t1", "TLC_468t2" }.Contains(m.Card.Id)) == 2, "full board prevents additional summons");
        var board = DeathrattleSlotBoard(p, () => source = Summon(p, id)); Kill(g, source);
        CheckDeathrattleSlot(p, board, new[] { "TLC_468t1", "TLC_468t2" }.Select(c => p.BoardZone.Single(m => m.Card.Id == c)),
            "Blobs fill the dying source's physical slot rightward past a location");
        ParkBoard(p);
        End(g);
    });
}
