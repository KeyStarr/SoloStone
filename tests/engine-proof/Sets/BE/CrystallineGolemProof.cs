using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CrystallineGolemProof
{
    internal static void Run() => StandardSetsProof.Case("BE_861", (g, p, id) => {
        p.Hero.Damage = 12;
        var left = Summon(p, "CS2_182"); var source = (Minion)Add(p, id);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play accepted");
        var right = Summon(p, "CS2_182");
        Core2026Proof.Check(source.HasTaunt && source[GameTag.REBORN] == 1 && source.IsRace(Race.ELEMENTAL), "printed Taunt, Reborn and Elemental");
        var clone = g.CloneExact(history: true); Kill(clone, (Minion)clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(clone.Player1.Hero.Damage == 9 && p.Hero.Damage == 12 && source.Zone == p.BoardZone, "cloned deathrattle and Reborn isolated");
        Kill(g, source); var reborn = p.BoardZone.Single(m => m.Card.Id == id);
        Core2026Proof.Check(p.Hero.Damage == 9 && p.Opponent.Hero.Damage == 0, "deathrattle heals owner three");
        Core2026Proof.Check(reborn.Id != source.Id && reborn.Card.AssetId == 133415 && reborn.AttackDamage == 2 && reborn.Health == 1
            && reborn.BaseHealth == 5 && reborn.HasTaunt && reborn[GameTag.REBORN] == 0
            && p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, reborn.Id, right.Id }), "fresh pinned one-Health copy in original position without Reborn");
        Kill(g, reborn);
        Core2026Proof.Check(p.Hero.Damage == 6 && p.BoardZone.All(m => m.Card.Id != id), "Reborn copy heals once but never reborns again");
        source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.Hero.Damage == 6 && p.BoardZone.All(m => m.Card.Id != id), "Silence removes deathrattle and Reborn");
        ClearBoard(g, p); p.Hero.Damage = 1; source = Summon(p, id);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Kill(g, source);
        Core2026Proof.Check(p.Hero.Damage == 0 && p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 1, "healing caps at full; Reborn respects full board's vacated slot");
        ClearBoard(g, p);
        p.Opponent.Hero.Damage = 8; source = Summon(p.Opponent, id); Kill(g, source);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 5 && p.Hero.Damage == 0, "opponent-owned deathrattle heals opponent");
        Play(g, p, "CS2_008", p.Opponent.Hero);
    });
}
