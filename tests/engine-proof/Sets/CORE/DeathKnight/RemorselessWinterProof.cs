using static Core2026Proof;

internal static class RemorselessWinterProof
{
    internal static void Run() => DeathKnightC2Proof.Case("RLK_709", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); var friendly = Summon(p, "CS2_182");
        var shield = Summon(p.Opponent, "CS2_182"); shield.HasDivineShield = true;
        var location = LocationProofs.Location(p.Opponent); int hand = p.HandZone.Count, deck = p.DeckZone.Count;
        DeathKnightC2Proof.Continue(g, copy => {
            Play(copy, copy.Player1, id);
            Core2026Proof.Check(copy.Player2.Hero.Damage == 2 && ((SabberStoneCore.Model.Entities.Minion)copy.IdEntityDic[enemy.Id]).Damage == 2
                && ((SabberStoneCore.Model.Entities.Minion)copy.IdEntityDic[friendly.Id]).Damage == 0
                && !((SabberStoneCore.Model.Entities.Minion)copy.IdEntityDic[shield.Id]).HasDivineShield
                && copy.IdEntityDic[location.Id][SabberStoneCore.Enums.GameTag.HEALTH] == location.Card.Health, "all enemies only, shields consumed, location ignored");
            Core2026Proof.Check(copy.Player1.HandZone.Count == hand + 1 && copy.Player1.DeckZone.Count == deck - 1, "draw after damage");
        });
        StandardSetsProof.ClearBoard(g, p.Opponent); Summon(p, "CS2_142"); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 5, "empty enemy board still hits hero with spell damage and draws");
        while (p.HandZone.Count < 9) Add(p, "CS2_182");
        var fullHandSpell = Add(p, id); PlayEntity(g, p, fullHandSpell);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.Opponent.Hero.Damage == 8, "casting from full hand frees slot before draw");
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        var discard = p.HandZone.First(); p.HandZone.Remove(discard); p.SetasideZone.Add(discard);
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.Damage == 1 && p.Opponent.Hero.Damage == 11, "empty deck fatigues after damage");
    });
}
