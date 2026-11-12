using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FrostwyrmsFuryProof
{
    internal static void Run() => DeathKnightC2Proof.Case("CORE_RLK_063", (g, p, id) => {
        DeathKnightC2Proof.Dependency("RLK_063t", 87085);
        var enemy = Summon(p.Opponent, "CS2_182"); var friendly = Summon(p, "CS2_182");
        var dormant = Summon(p.Opponent, "CS2_182"); dormant[GameTag.DORMANT] = 1;
        var location = LocationProofs.Location(p.Opponent);
        var spell = Add(p, id);
        var options = p.Options().Where(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target == p.Hero) && options.Any(t => t.Target == friendly)
            && options.Any(t => t.Target == p.Opponent.Hero) && options.Any(t => t.Target == enemy)
            && options.All(t => t.Target != dormant && t.Target?.Id != location.Id), "damage needs a targetable character");
        DeathKnightC2Proof.Continue(g, copy => {
            var cp = copy.Player1;
            PlayEntity(copy, cp, copy.IdEntityDic[spell.Id], cp.Opponent.Hero);
            Core2026Proof.Check(cp.Opponent.Hero.Damage == 5 && ((Minion)copy.IdEntityDic[enemy.Id]).IsFrozen
                && !((Minion)copy.IdEntityDic[dormant.Id]).IsFrozen && !((Minion)copy.IdEntityDic[friendly.Id]).IsFrozen,
                "damage then enemy-only freeze excludes Dormant/location");
            var token = cp.BoardZone.Last();
            Core2026Proof.Check(token.Card.Id == "RLK_063t" && token.AttackDamage == 5 && token.Health == 5
                && token.IsRace(Race.UNDEAD) && token.IsRace(Race.DRAGON), "exact 5/5 dual-race token appended");
        });
        ClearBoard(g, p); LocationProofs.Location(p);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.BoardZone.IsFull && p.BoardZone.SlotCount == 7 && p.BoardZone.All(m => m.Card.Id != "RLK_063t")
            && p.Opponent.Hero.Damage == 10, "mixed full board still deals damage/freezes without token");
        ClearBoard(g, p); Summon(p, "CS2_142");
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 16 && p.BoardZone.Last().AttackDamage == 5 && p.BoardZone.Last().Health == 5,
            "spell damage scales damage, not Frostwyrm stats");
        ClearBoard(g, p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        Play(g, p, id, p.Hero);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.GetSlot(6).Card.Id == "RLK_063t",
            "one free slot beside location receives appended Frostwyrm");
    });
}
