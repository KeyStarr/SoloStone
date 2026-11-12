using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CinderfinProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_225", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play accepted");
        Kill(g, source); var cinder = p.BoardZone.Single(m => m.Card.Id == "TLC_249");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, cinder.Id, right.Id }), "Cinder replaces dead middle minion");
        Core2026Proof.Check(cinder.Card.Id == "TLC_249" && cinder.AttackDamage == 2 && cinder.Health == 1 && cinder.HasDeathrattle && cinder.Card.Implemented, "kill summons real 2/1 Sizzling Cinder with its power");
        Kill(g, cinder); Core2026Proof.Check(p.Opponent.Hero.Damage == 2, "summoned Cinder deathrattle works");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 1 && p.BoardZone.Single().Card.Id == "BE_228", "silence removes summon deathrattle");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "TLC_249") == 1, "full board frees source slot before deathrattle summon");
        End(g); // Native decision boundary settles summoned-minion bookkeeping after direct kill.
    });
}
