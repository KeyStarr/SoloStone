using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ReluctantWranglerProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_443", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play");
        Core2026Proof.Check(source[GameTag.REBORN] == 1 && source.HasDeathrattle, "printed Reborn and Deathrattle");
        var clone = g.Clone(); Kill(clone, (Minion)clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(p.BoardZone.Count == 3 && clone.Player1.BoardZone.Count == 4, "clone Reborn isolated");
        Kill(g, source);
        var token = p.BoardZone.Single(m => m.Card.Id == "TLC_443t");
        var reborn = p.BoardZone.Single(m => m.Card.Id == id);
        Core2026Proof.Check(token.AttackDamage == 2 && token.Health == 2 && token.HasTaunt && token.IsRace(Race.UNDEAD) && token.IsRace(Race.BEAST) && token.Card.Implemented, "exact 2/2 Taunt Undead Beast");
        Core2026Proof.Check(reborn.Health == 1 && reborn[GameTag.REBORN] == 0 && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] {left.Id, reborn.Id, token.Id, right.Id}), "Deathrattle then Reborn at dead source position");
        Kill(g, reborn); Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "TLC_443t") == 2 && !p.BoardZone.Any(m => m.Card.Id == id), "Reborn death repeats Deathrattle once");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 0, "Silence removes Reborn and Deathrattle");
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "TLC_443t") == 1 && !p.BoardZone.Any(m => m.Card.Id == id), "Deathrattle fills freed last slot; no room for Reborn");
        End(g);
    });
}
