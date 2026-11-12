using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class HaywireHornswogProof
{
    internal static void Run() => StandardSetsProof.Case("END_030", (g, p, id) => {
        var undiscounted = Add(p, id);
        Core2026Proof.Check(undiscounted.Cost == 6 && undiscounted[GameTag.COST] == 6, "no Overload means full visible cost");
        Play(g, p, "EX1_241", p.Opponent.Hero); // Lava Burst: Overload (2).
        var held = Add(p, id);
        Core2026Proof.Check(p.OverloadThisGame == 2 && held.Cost == 4 && undiscounted.Cost == 4,
            "counts crystals before and after entering hand");
        Core2026Proof.Check(held.Card.IsRace(Race.BEAST) && held.Card.IsRace(Race.DRAGON)
            && !held.Card.IsRace(Race.PIRATE), "both printed types, no unrelated type");
        Play(g, p, "EX1_238", p.Opponent.Hero);
        Core2026Proof.Check(held.Cost == 3 && held[GameTag.COST] == 3, "new friendly Overload changes visible cost");
        End(g);
        Core2026Proof.Check(p.OverloadThisGame == 3 && held.Cost == 3, "discount survives turn end");
        Play(g, p.Opponent, "EX1_238", p.Hero);
        Core2026Proof.Check(held.Cost == 3 && p.OverloadThisGame == 3, "opponent Overload ignored");
        End(g);
        Core2026Proof.Check(p.OverloadOwed == 0 && p.OverloadLocked == 3 && held.Cost == 3,
            "discount survives owed crystals moving to locked at own turn start");
        var clone = g.Clone();
        Play(clone, clone.Player1, "EX1_241", clone.Player2.Hero);
        Core2026Proof.Check(clone.IdEntityDic[held.Id][GameTag.COST] == 1 && held[GameTag.COST] == 3
            && p.OverloadThisGame == 3, "clone cost and lifetime counter isolated");
        var minion = (Minion)held;
        PlayEntity(g, p, held);
        Core2026Proof.Check(minion.AttackDamage == 4 && minion.Health == 6 && minion.HasTaunt
            && minion.CantBeTargetedBySpells && minion.CantBeTargetedByHeroPowers,
            "discounted play retains stats, Taunt and Elusive");
        Play(g, p, "DS1_070", minion);
        Core2026Proof.Check(minion.AttackDamage == 6 && minion.Health == 8,
            "Houndmaster targets secondary Beast type through Elusive");
        Play(g, p, "BE_228", minion);
        Core2026Proof.Check(minion.IsSilenced && !minion.HasTaunt && !minion.CantBeTargetedBySpells
            && minion.IsRace(Race.BEAST) && minion.IsRace(Race.DRAGON), "Silence removes keywords, keeps both types");
        Kill(g, minion);
        Play(g, p, "EX1_241", p.Opponent.Hero);
        Play(g, p, "EX1_241", p.Opponent.Hero);
        Core2026Proof.Check(p.OverloadThisGame == 7 && undiscounted.Cost == 0 && undiscounted[GameTag.COST] == 0,
            "removed source does not affect another copy; cost floors at zero");
        var fresh = Add(p, id);
        Core2026Proof.Check(fresh.Cost == 0, "fresh copy initializes from lifetime total above printed cost");
        PlayEntity(g, p, fresh);
        Core2026Proof.Check(p.UsedMana == 0, "zero-cost Hornswog pays zero mana");
        StandardSetsProof.Elusive(g, p, (Minion)fresh);
    });
}
