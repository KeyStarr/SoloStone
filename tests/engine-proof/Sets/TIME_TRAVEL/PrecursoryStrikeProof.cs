using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PrecursoryStrikeProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_750", (g, p, id) => {
        var cheap = (Minion)Add(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        int deck = p.DeckZone.Count; Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Damage == 3 && p.DeckZone.Count == deck, "four-cost held minion does not qualify");
        var expensiveSpell = Add(p, "CS2_032"); deck = p.DeckZone.Count;
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.DeckZone.Count == deck, "expensive spell does not qualify");
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        var drawn = Entity.FromCard(p, Cards.FromId("CS2_168")); p.DeckZone.Add(drawn);
        var topSpell = Entity.FromCard(p, Cards.FromId("CS2_029")); p.DeckZone.Add(topSpell);
        Core2026Proof.Check(p.DeckZone.TopCard == topSpell, "mixed deck has spell on top");
        var expensive = (Minion)Add(p, "TIME_856"); deck = p.DeckZone.Count;
        Play(g, p, id, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == 3 && p.DeckZone.Count == deck - 1 && p.HandZone.Last() == drawn
            && drawn.Card.Id == "CS2_168" && topSpell.Zone == p.DeckZone, "five-cost hand minion draws exact minion past top spell; friendly hero target allowed");
        int hand = p.HandZone.Count, fatigue = p.Hero.Fatigue, damage = p.Hero.Damage;
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.DeckZone.Count == 1 && p.DeckZone.TopCard == topSpell && p.HandZone.Count == hand
            && p.Hero.Fatigue == fatigue && p.Hero.Damage == damage, "spell-only deck draws nothing and causes no fatigue");
        p.DeckZone.Remove(topSpell); p.SetasideZone.Add(topSpell);
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.DeckZone.Count == 0 && p.HandZone.Count == hand && p.Hero.Fatigue == fatigue
            && p.Hero.Damage == damage, "empty filtered draw causes no fatigue");
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182")));
        expensive[GameTag.COST] = 4; deck = p.DeckZone.Count;
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.DeckZone.Count == deck, "current discounted cost below five does not qualify");
        PlayEntity(g, p, expensive); PlayEntity(g, p, cheap); deck = p.DeckZone.Count;
        Play(g, p, id, cheap);
        Core2026Proof.Check(cheap.Zone == p.GraveyardZone && p.DeckZone.Count == deck, "board minion excluded; spell power applies to damage");
    });
}
