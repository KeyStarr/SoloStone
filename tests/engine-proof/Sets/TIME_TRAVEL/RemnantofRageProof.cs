using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static EmeraldDreamBatch3Proof;

internal static class RemnantofRageProof
{
    internal static void Run() => StandardSetsProof.Case("END_004", (g, p, id) => {
        var held = Add(p, id); Cost(held, 7);
        Core2026Proof.Check(held.Card.IsRace(Race.UNDEAD) && held.Card.IsRace(Race.DEMON) && !held.Card.IsRace(Race.BEAST), "both printed types");
        Kill(g, Summon(p, "CS2_182")); Cost(held, 6);
        Kill(g, Summon(p.Opponent, "CS2_182")); Cost(held, 5); Cost(Add(p, id), 5);
        Core2026Proof.Check(g.NumMinionsKilledThisTurn == 2, "actual deaths on both sides update counter");
        var clone = g.CloneExact(history: true); Kill(clone, Summon(clone.Player2, "CS2_182"));
        Cost(clone.IdEntityDic[held.Id], 4); Cost(held, 5);
        int hand = p.HandZone.Count; PlayEntity(g, p, held);
        Core2026Proof.Check(p.HandZone.Count == hand + 1 && ((Minion)held).AttackDamage == 5 && ((Minion)held).Health == 4, "discounted Battlecry draws two");
        ((Minion)held).Silence(); End(g); Cost(p.HandZone.Single(c => c.Card.Id == id), 7);
        Kill(g, Summon(p.Opponent, "CS2_182")); Cost(p.HandZone.Single(c => c.Card.Id == id), 6);
        End(g); var fresh = Add(p, id); Cost(fresh, 7);
        for (int i = 0; i < 8; i++) Kill(g, Summon(p, "CS2_182")); Cost(fresh, 0);
        Kill(g, (Minion)held); Cost(fresh, 0);
        EmptyDeck(p); p.Hero.Damage = 0; PlayEntity(g, p, fresh);
        Core2026Proof.Check(p.Hero.Damage == 3 && p.Hero.Fatigue == 2, "two draw attempts fatigue independently");
    });
}
