using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MountainBearProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_AV_337", (g, p, id) => {

        var left = Summon(p, "CS2_182"); var source = (Minion)Play(g, p, id); var right = Summon(p, "CS2_182");
        Core2026Proof.Check(source.HasTaunt && source.HasDeathrattle && source.IsRace(Race.BEAST), "Bear keywords and race");
        Kill(g, source);
        var cubs = p.BoardZone.Where(m => m.Card.Id == "AV_337t").OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(cubs.Length == 2 && cubs.All(m => m.AttackDamage == 2 && m.Health == 4 && m.HasTaunt && m.IsRace(Race.BEAST) && m.Card.Implemented), "two exact 2/4 Taunt Beast Cubs");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(cubs.Select(m => m.Id).Concat(new[] { left.Id, right.Id })), "dead leftmost source's slot takes Cubs in creation order");
        var board = DeathrattleSlotBoard(p, () => source = Summon(p, id)); Kill(g, source);
        cubs = p.BoardZone.Where(m => m.Card.Id == "AV_337t").OrderBy(m => m.Id).ToArray();
        CheckDeathrattleSlot(p, board, cubs, "Cubs fill the dying Bear's physical slot rightward past a location");
        ParkBoard(p);
        ClearBoard(g, p); source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 0, "Silence removes deathrattle");
        left = Summon(p, "CS2_182"); left[GameTag.DORMANT] = 1; source = Summon(p, id); right = Summon(p, "CS2_182");
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack()); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        cubs = p.BoardZone.Where(m => m.Card.Id == "AV_337t").OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id }.Concat(cubs.Select(m => m.Id)).Append(right.Id)), "in-play source uses actual position including Dormant neighbour");
        left[GameTag.DORMANT] = 0; source.Silence(); ClearBoard(g, p);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182"); source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "AV_337t") == 1, "deathrattle fills only available board slot");
        source.Card.Power.DeathrattleTask.Process(g, p, source, null, new TaskStack()); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count == 7, "full board rejects all additional Cubs");
    });
}
