using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static EmeraldDreamBatch3Proof;

internal static class TormentedDreadwingProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_572", (g, p, id) => {
        EmptyDeck(p); var dragon1 = InDeck(p, "TIME_056"); var dragon2 = InDeck(p, "EDR_890"); var other = InDeck(p, "CS2_182");
        var held = Add(p, "TIME_056"); var enemy = InDeck(p.Opponent, "EDR_890");
        var source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(dragon1.Zone == p.HandZone && dragon2.Zone == p.HandZone && other.Zone == p.DeckZone && enemy.Zone == p.Opponent.DeckZone, "draw two friendly Dragons only");
        Cost(dragon1, 2); Cost(dragon2, 2); Cost(held, 3);
        CopyCost(dragon1, 2); CopyCost(dragon2, 2); PublishedCost(dragon1, 2); PublishedCost(dragon2, 2);
        End(g); End(g); Cost(dragon1, 2);
        int health = p.Hero.Health; int deckCount = p.DeckZone.Count; source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(p.Hero.Health == health && p.DeckZone.Count == deckCount, "no eligible Dragon: no draw or fatigue");
        var only = InDeck(p, "EDR_890"); source = Summon(p, id); source.Silence(); Kill(g, source); Core2026Proof.Check(only.Zone == p.DeckZone, "Silence suppresses draw");
        while (p.HandZone.Count < 10) Add(p, "CS2_182"); source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(only.Zone == p.GraveyardZone && p.HandZone.Count == 10, "full hand burns only Dragon"); Cost(only, 3);
        EmptyDeck(p); foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        only = InDeck(p, "TIME_056"); source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(only.Zone == p.HandZone && p.HandZone.Count == 1, "one match draws once"); Cost(only, 2);
        foreach (bool history in new[] { false, true })
        {
            var next = HeraldProof.New(CardClass.DRUID).CloneExact(history: history); var owner = next.Player1;
            EmptyDeck(owner);
            var first = InDeck(owner, "EDR_260t"); var second = InDeck(owner, "EDR_260t");
            var tutor = Summon(owner, id); int draws = owner.NumCardsDrawnThisTurn;
            Kill(next, tutor);
            Core2026Proof.Check(first.Zone == owner.BoardZone && second.Zone == owner.BoardZone
                && owner.NumCardsDrawnThisTurn == draws + 2 && owner.Hero.Fatigue == 1,
                "Illusion redraw consumes the second tutor selection once, without a stale second draw");
        }

    });
}
