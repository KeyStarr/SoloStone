using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TheEggOfKhelosProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_410", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var source = (Minion)Play(g, p, id); var right = Summon(p, "CS2_182");
        foreach (string next in new[] { "DINO_410t2", "DINO_410t3", "DINO_410t4", "DINO_410t5", "DINO_410t" })
        {
            Kill(g, source); source = p.BoardZone.Single(m => m.Card.Id == next);
            Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { source.Id, left.Id, right.Id }), "removed leftmost Egg's slot takes next stage");
            Core2026Proof.Check(source.Card.Implemented && source.AttackDamage == (next == "DINO_410t" ? 20 : 0) && source.Health == (next == "DINO_410t" ? 20 : 3), "pinned stage identity and stats");
        }
        Core2026Proof.Check(source.HasTaunt && source.IsRace(Race.BEAST), "Khelos is 20/20 Taunt Beast");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 1, "silenced Egg does not progress");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone[0].Card.Id == "DINO_410t2", "full board uses one freed slot");
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.BoardZone.Count == 7, "full board rejects extra stage");
        foreach (var minion in p.BoardZone.ToArray()) minion.Silence();
        ClearBoard(g, p); ClearBoard(g, p.Opponent);
        left = Summon(p, "CS2_182"); source = Summon(p, id); right = Summon(p, "CS2_182");
        left[GameTag.DORMANT] = 1;
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        var generated = p.BoardZone.Where(m => new[] { "DINO_410t2" }.Contains(m.Card.Id)).OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(generated.Length == 1 && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id }.Concat(generated.Select(m => m.Id)).Append(right.Id)), "live source summons right in creation order with Dormant left neighbour");
        left[GameTag.DORMANT] = 0;
        while (p.BoardZone.Count < 7) Summon(p, "CS2_182");
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => new[] { "DINO_410t2" }.Contains(m.Card.Id)) == 1, "full board prevents additional summons");
        foreach (var minion in p.BoardZone.ToArray()) minion.Silence();
        var board = DeathrattleSlotBoard(p, () => source = Summon(p, id));
        foreach (string next in new[] { "DINO_410t2", "DINO_410t3", "DINO_410t4", "DINO_410t5", "DINO_410t" })
        {
            Kill(g, source); source = p.BoardZone.Single(m => m.Card.Id == next);
            CheckDeathrattleSlot(p, board, new[] { source }, "next stage fills the dying Egg's physical slot");
        }
        ParkBoard(p);
        End(g);
    });
}
