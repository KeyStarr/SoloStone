using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RelicMinerProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_109", (g, p, id) => {
        var declarations = OfflinePoolDeclarations.Cards.Single(d => d.CardId == id).Pools;
        foreach (var pair in new[] { (Rarity.COMMON, "CORE_CS2_122"), (Rarity.RARE, "CORE_EX1_043"), (Rarity.EPIC, "CORE_EX1_005"), (Rarity.LEGENDARY, "CORE_EX1_014") })
        {
            ParkBoard(p); PoolPilotProof.EmptyHand(p); PoolPilotProof.ClassDeck(p);
            var deck = MillDeck(p, pair.Item2); var milled = deck[2];
            Core2026Proof.Check(milled.Card.Rarity == pair.Item1, "fixture rarity");
            var pool = declarations.Single(d => d.Filter.Rarity == pair.Item1).Filter;
            foreach (var hero in new[] { CardClass.DEATHKNIGHT, CardClass.DEMONHUNTER, CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE, CardClass.PALADIN, CardClass.PRIEST, CardClass.ROGUE, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.WARRIOR })
                Core2026Proof.Check(OfflineCardPools.Candidates(g, hero, pool, Cards.FromId(id).AssetId).Count >= 3, "rarity pool floor for every playable class");
            var source = Play(g, p, id); var offers = p.Choice.Choices.ToArray();
            var candidates = OfflineCardPools.Candidates(g, p.HeroClass, pool, source.Card.AssetId);
            Core2026Proof.Check(milled.Zone == p.GraveyardZone && p.DeckZone.Select(e => e.Id).SequenceEqual(deck.Take(2).Select(e => e.Id))
                && p.DeckZone.TopCard.Id == deck[1].Id && p.HandZone.Count == 0
                && offers.Length == 3 && offers.Select(x => g.IdEntityDic[x].Card.Id).Distinct().Count() == 3
                && offers.All(x => candidates.Any(c => c.Id == g.IdEntityDic[x].Card.Id && c.Rarity == pair.Item1 && c.Id != id)),
                "top identity destroyed before three distinct admitted same-rarity controller/neutral offers");
            int selected = offers[2]; string chosen = g.IdEntityDic[selected].Card.Id;
            PoolPilotProof.Resume(g, next => { PlayerDecisionProof.Answer(next, selected);
                Core2026Proof.Check(next.Player1.HandZone.Single().Card.Id == chosen && next.Player1.Choice == null
                    && next.Player1.DeckZone.Select(e => e.Id).SequenceEqual(deck.Take(2).Select(e => e.Id)),
                    "selected same-rarity card added once across clone/snapshot; remaining deck order unchanged"); });
        }
        ParkBoard(p); PoolPilotProof.EmptyHand(p); PoolPilotProof.ClassDeck(p);
        var rng = g.Random.CaptureState(); int damage = p.Hero.Damage;
        Play(g, p, id);
        Core2026Proof.Check(p.Choice == null && p.Hero.Damage == damage && g.Random.CaptureState().Equals(rng), "empty deck has no mill, fatigue, choice or RNG");
        ParkBoard(p); var freeDeck = MillDeck(p, "CS2_033"); var free = freeDeck[2];
        Core2026Proof.Check(free.Card.Rarity == Rarity.FREE, "FREE fixture"); rng = g.Random.CaptureState();
        Play(g, p, id);
        Core2026Proof.Check(free.Zone == p.GraveyardZone && p.Choice == null && g.Random.CaptureState().Equals(rng)
            && p.DeckZone.Select(e => e.Id).SequenceEqual(freeDeck.Take(2).Select(e => e.Id)), "exact FREE top card destroyed with no Standard FREE offers; remaining order unchanged");
        ParkBoard(p); var fullHandDeck = MillDeck(p, "CORE_CS2_122"); var top = fullHandDeck[2];
        var spell = (Minion)Play(g, p, id); int chosenId = p.Choice.Choices.First(); PoolPilotProof.FillHand(p);
        PlayerDecisionProof.Answer(g, chosenId);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.IdEntityDic[chosenId].Zone == p.GraveyardZone && top.Zone == p.GraveyardZone
            && p.DeckZone.Select(e => e.Id).SequenceEqual(fullHandDeck.Take(2).Select(e => e.Id)), "full hand burns chosen card after exact top mill; remaining order unchanged");
        ParkBoard(p); PoolPilotProof.EmptyHand(p); var summonDeck = MillDeck(p, "CORE_CS2_122");
        Summon(p, id); Core2026Proof.Check(p.Choice == null && p.DeckZone.Select(e => e.Id).SequenceEqual(summonDeck.Select(e => e.Id)), "summon does not mill any card or change deck order");
    });

    private static IPlayable[] MillDeck(Controller p, string topId)
    {
        PoolPilotProof.ClassDeck(p);
        var top = Cards.FromId(topId);
        var deck = new[] { "CORE_CS2_122", "CORE_EX1_043", "CORE_EX1_005" }.Select(Cards.FromId)
            .Where(c => c.Rarity != top.Rarity).Take(2).Append(top).Select(c => Entity.FromCard(p, c)).ToArray();
        foreach (var card in deck) p.DeckZone.Add(card);
        Core2026Proof.Check(deck.Select(e => e.Card.Rarity).Distinct().Count() == 3 && p.DeckZone.TopCard.Id == deck[2].Id,
            "three-card fixture has different rarities and explicit top entity");
        return deck;
    }
}
