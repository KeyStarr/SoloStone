using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SteamfinThiefProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_429", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 1, "no previous-turn Murloc means no tokens");
        ClearBoard(g, p); Play(g, p, "CS2_168"); End(g); End(g); ClearBoard(g, p);
        var left = Summon(p, "CS2_182"); left[GameTag.DORMANT] = 1; var right = Summon(p, "CS2_182");
        source = (Minion)Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play accepted");
        var tokens = p.BoardZone.Where(m => m.Card.Id == "TLC_429t").OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(tokens.Length == 2 && tokens.All(m => m.AttackDamage == 1 && m.Health == 1 && m.IsRush && m.IsRace(Race.MURLOC) && m.Card.Implemented), "two exact printed-Rush 1/1 Murlocs");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id }.Concat(tokens.Select(m => m.Id)).Append(right.Id)), "actual position beside source, creation order");
        left[GameTag.DORMANT] = 0; ClearBoard(g, p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "TLC_429t") == 1, "board cap truncates tokens");
        ClearBoard(g, p); source = Summon(p, id); Kill(g, source);
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.All(m => m.Card.Id == "TLC_429t"), "removed source appends tokens");
        ClearBoard(g, p); End(g); End(g); End(g); Play(g, p.Opponent, "CS2_168"); End(g); Play(g, p, "CS2_168"); Play(g, p, id);
        Core2026Proof.Check(!p.BoardZone.Any(m => m.Card.Id == "TLC_429t"), "opponent previous-turn and own current-turn Murlocs insufficient");
    });
}
