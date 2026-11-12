using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SkyscreamerEggsProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_237", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play accepted");
        Core2026Proof.Check(p.BoardZone.Count == 3 && source.HasDeathrattle, "native keywords; no battlecry summon");
        Kill(g, source);
        var tokens = p.BoardZone.Where(m => m.Card.Id == "TLC_237t").ToArray();
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id }.Concat(tokens.Select(m => m.Id)).Append(right.Id)), "Skyscreamers replace dead middle minion between neighbours");
        Core2026Proof.Check(tokens.Length == 4 && tokens.All(m => m.Card.Id == "TLC_237t" && m.AttackDamage == 2 && m.Health == 1), "kill summons pinned tokens with exact stats and keywords");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 1 && p.BoardZone.Single().Card.Id == "BE_228", "silence removes deathrattle");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "TLC_237t") == 1, "full board permits only freed deathrattle slot");
        End(g);
    });
}
