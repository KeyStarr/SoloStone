using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MorchokProof
{
    // STILL-UNPINNED: data/rules-evidence/cost-sampling/{excess-reduction-budget,morchok-failed-draw}.yaml.
    // Tier C Morchok/DC3-DC4, tier E reveal/burn reports; owner-approved working assumptions.
    internal static void Run() => StandardSetsProof.Case("CATA_570", Unpinned_Morchok);
    private static void Deck(Controller p, params string[] topFirst)
    {
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        foreach (string id in topFirst.Reverse()) Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
    }
    private static void Reset(Controller p)
    {
        ParkBoard(p); PoolPilotProof.EmptyHand(p);
    }
    private static void Unpinned_Morchok(Game g, Controller p, string id)
    {
        Deck(p, "CS2_125", "CS2_029", "CS2_182"); // 3,4,4: discounts 3,4,3.
        var source = Add(p, id);
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
        Core2026Proof.Check(p.BoardZone.Single().Card.Id == id && p.BoardZone.Single().AttackDamage == 10
            && p.BoardZone.Single().Health == 10 && p.BoardZone.Single().Card.IsRace(Race.ELEMENTAL), "Morchok 10/10 Elemental");
        Core2026Proof.Check(p.HandZone.Select(c => c.Card.Id).SequenceEqual(new[] { "CS2_125", "CS2_029", "CS2_182" })
            && p.HandZone.Select(c => c.Cost).SequenceEqual(new[] { 0, 0, 1 })
            && p.HandZone.All(c => c[GameTag.COST] == c.Cost), "one carried budget, visible discounts, normal deck order");
        Core2026Proof.Check(g.PowerHistory.Full.OfType<PowerHistoryMetaData>().Any(m => m.Type == MetaDataType.TARGET
            && m.Info.SequenceEqual(new[] { source.Id })), "authored draw impact has source TARGET");
        var discounted = p.HandZone.Last(); Kill(g, (Minion)source); End(g); End(g);
        Core2026Proof.Check(discounted.Cost == 1, "discount survives source death and turn end");
        Reset(p); Deck(p, "CS2_029", "CS2_200", "CS2_182"); // 4+6 exactly exhausts.
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone.All(c => c.Cost == 0)
            && p.DeckZone.Single().Card.Id == "CS2_182", "exact budget stops before next draw");
        Reset(p); Deck(p, "GAME_005", "GAME_005", "CS2_200");
        int fatigue = p.Hero.Fatigue, damage = p.Hero.Damage;
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 3 && p.HandZone.All(c => c.Cost == 0)
            && p.DeckZone.IsEmpty && p.Hero.Fatigue == fatigue && p.Hero.Damage == damage,
            "zero-cost successes leave budget; exhausted deck stops without fatigue");
        Reset(p); Deck(p); Play(g, p, id);
        Core2026Proof.Check(p.HandZone.IsEmpty && p.Hero.Fatigue == fatigue && p.Hero.Damage == damage, "initially empty deck does not attempt fatigue");
        Reset(p); Deck(p, "CS2_029", "CS2_200", "CS2_182", "GAME_005");
        source = Add(p, id); PoolPilotProof.FillHand(p);
        int discards = p.DiscardedEntities.Count;
        PlayEntity(g, p, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Last().Card.Id == "CS2_029" && p.HandZone.Last().Cost == 0
            && p.DeckZone.IsEmpty && p.GraveyardZone.Count(c => new[] { "CS2_200", "CS2_182", "GAME_005" }.Contains(c.Card.Id)) == 3
            && p.DiscardedEntities.Count == discards && p.Hero.Fatigue == fatigue, "burns keep budget and consume remaining deck without discard/fatigue");
        Reset(p); Deck(p, "CS2_029", "CS2_200", "CS2_182", "CS2_182");
        var apprentice = Summon(p, "EX1_608"); Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Select(c => c.Cost).SequenceEqual(new[] { 0, 0, 3 })
            && p.DeckZone.Count == 1, "current hand-entry cost under aura consumes 3 then 6 then 1: "
                + string.Join(",", p.HandZone.Select(c => c.Card.Id + ":" + c.Cost)) + " deck=" + p.DeckZone.Count);
        apprentice.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.HandZone.First().Cost == 1, "aura removal exposes permanent reduction rather than a permanent zero SET");
        Reset(p); Deck(p, "CATA_820", "CS2_200", "CS2_182");
        var heldLeft = Add(p, "CS2_182"); var heldRight = Add(p, "CS2_120");
        var shatterSource = Add(p, id);
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[shatterSource.Id]));
        Core2026Proof.Check(p.HandZone.Count == 5 && p.HandZone.Count(c => c.Card.Id is "CATA_820t" or "CATA_820t2") == 2
            && p.HandZone.Where(c => c.Card.Id is "CATA_820t" or "CATA_820t2" or "CS2_200").All(c => c.Cost == 0)
            && heldLeft.Cost == 4 && heldRight.Cost == 2 && p.DeckZone.Single().Card.Id == "CS2_182",
            "Shatter parent spends four budget before splitting; both halves inherit discount and next six exhausts remainder");
        Reset(p); Deck(p, "CS2_182"); Summon(p, id);
        Core2026Proof.Check(p.HandZone.IsEmpty && p.DeckZone.Count == 1, "summon without Battlecry does not draw");
    }
}
