using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class HarbingerOfWinterProof
{
    internal static void Run() => DeathKnightC2Proof.Case("RLK_511", (g, p, id) => {
        var frost = Entity.FromCard(p, Cards.FromId("RLK_709")); p.DeckZone.Add(frost);
        var source = (Minion)Play(g, p, id); int hand = p.HandZone.Count;
        Core2026Proof.Check(p.HandZone.Count == hand && source.AttackDamage == 3 && source.Health == 2, "3/2 without Battlecry draw");
        DeathKnightC2Proof.Continue(g, copy => {
            var cp = copy.Player1; Kill(copy, (Minion)copy.IdEntityDic[source.Id]);
            Core2026Proof.Check(copy.IdEntityDic[frost.Id].Zone == cp.HandZone && cp.HandZone.Count == hand + 1,
                "tutor moves existing matching deck entity");
        });
        int fatigue = p.Hero.Damage; hand = p.HandZone.Count; source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == hand && p.Hero.Damage == fatigue, "no Frost spell does nothing, no fatigue");
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == hand && p.Hero.Damage == fatigue, "empty deck does not fatigue");
        var legacy = Entity.FromCard(p, Cards.FromId("CS2_024")); p.DeckZone.Add(legacy);
        var fire = Entity.FromCard(p, Cards.FromId("CS2_029")); p.DeckZone.Add(fire);
        source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(legacy.Zone == p.HandZone && fire.Zone == p.DeckZone, "legacy Frostbolt metadata recognized; Fire excluded");
        var one = Entity.FromCard(p, Cards.FromId("RLK_709")); p.DeckZone.Add(one);
        var two = Entity.FromCard(p, Cards.FromId("CS2_024")); p.DeckZone.Add(two);
        source = Summon(p, id); int sourceId = source.Id;
        DeathKnightC2Proof.Continue(g, copy => {
            Kill(copy, (Minion)copy.IdEntityDic[sourceId]);
            Core2026Proof.Check(new[] { one.Id, two.Id }.Count(n => copy.IdEntityDic[n].Zone == copy.Player1.HandZone) == 1
                && copy.IdEntityDic[fire.Id].Zone == copy.Player1.DeckZone, "random tutor chooses exactly one eligible existing entity");
        });
        foreach (var card in p.DeckZone.Where(c => c.Card.Id != "CS2_029").ToArray())
        { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        var burn = Entity.FromCard(p, Cards.FromId("RLK_709")); p.DeckZone.Add(burn);
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(burn.Zone == p.GraveyardZone && p.HandZone.Count == 10 && fire.Zone == p.DeckZone, "full hand burns matching tutor");
        source = Summon(p, id); source.Silence(); g.DeathProcessingAndAuraUpdate();
        var untouched = Entity.FromCard(p, Cards.FromId("RLK_709")); p.DeckZone.Add(untouched); Kill(g, source);
        Core2026Proof.Check(untouched.Zone == p.DeckZone, "silence removes tutor");
    });
}
