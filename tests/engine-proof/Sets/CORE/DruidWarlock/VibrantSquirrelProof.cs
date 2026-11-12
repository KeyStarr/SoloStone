using SabberStoneCore.Model.Zones;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class VibrantSquirrelProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_SW_439", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 1 && source.IsRace(Race.BEAST), "printed 2/1 Beast");
        int count = p.DeckZone.Count; Kill(g, source);
        var acorns = p.DeckZone.Where(c => c.Card.Id == "SW_439t").ToArray();
        Core2026Proof.Check(p.DeckZone.Count == count + 4 && acorns.Length == 4 && acorns.All(c => c.Card.Implemented && c.Cost == 1
            && c.Card[GameTag.CASTSWHENDRAWN] == 1), "death shuffles four exact Acorns");
        foreach (var acorn in acorns) { p.DeckZone.Remove(acorn); p.DeckZone.Add(acorn, 0); }
        var clone = g.CloneExact(history: true); Generic.Draw(clone.Player1, clone.IdEntityDic[acorns[0].Id]); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(clone.Player1.BoardZone.Single().Card.Id == "SW_439t2" && p.BoardZone.Count == 0
            && acorns[0].Zone == p.DeckZone, "draw summons only in clone");
        var left = Summon(p, "CS2_182"); Generic.Draw(p, acorns[0]); g.DeathProcessingAndAuraUpdate();
        var squirrel = p.BoardZone.Last();
        Core2026Proof.Check(squirrel.Card.Id == "SW_439t2" && squirrel.Card.Implemented && squirrel.AttackDamage == 2 && squirrel.Health == 1
            && squirrel.IsRace(Race.BEAST) && p.BoardZone.First() == left && acorns[0].Zone != p.HandZone
            && p.HandZone.Single().Card.Id == "CS2_182", "Acorn appends exact Squirrel and replacement draw");
        count = p.DeckZone.Count; Play(g, p, "SW_439t");
        Core2026Proof.Check(p.BoardZone.Count == 3 && p.DeckZone.Count == count && p.HandZone.Count == 1, "manual Acorn summons without replacement draw");
        while (p.BoardZone.Count < 7) Summon(p, "CS2_182");
        int hand = p.HandZone.Count; Generic.Draw(p, acorns[1]); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.HandZone.Count == hand + 1, "full board still gets replacement draw");
        while (p.HandZone.Count < 10) Add(p, "CS2_182"); count = p.DeckZone.Count;
        Generic.Draw(p, acorns[2]); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.HandZone.Count == 10 && p.DeckZone.Count == count - 1
            && acorns[2].Zone == p.GraveyardZone, "full hand burns Acorn without cast or replacement");
        ClearBoard(g, p);
        source = Summon(p, id); source.Silence(); count = p.DeckZone.Count; Kill(g, source);
        Core2026Proof.Check(p.DeckZone.Count == count, "Silence removes shuffle Deathrattle");
        foreach (var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
        foreach (var c in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(c));
        Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        for (int i = 0; i < 2; i++) Entity.FromCard(p, Cards.FromId("SW_439t"), zone: p.DeckZone);
        Generic.Draw(p); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.All(m => m.Card.Id == "SW_439t2")
            && p.HandZone.Single().Card.Id == "CS2_182" && p.DeckZone.Count == 0, "consecutive Acorns cast and replace each draw");
        while (p.DeckZone.Count < (DeckZone.DeckMaximumCapcity - 1)) Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(p.DeckZone.Count == DeckZone.DeckMaximumCapcity && p.DeckZone.Count(c => c.Card.Id == "SW_439t") == 1, "shuffle respects deck capacity");
    });
}
