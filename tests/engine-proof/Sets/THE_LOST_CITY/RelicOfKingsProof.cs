using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static Core2026Proof;
using static LostCityPoolProof;

internal static class RelicOfKingsProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_334", (g, p, id) => {
        var replay = GiftedDiscoverProof.New(g.Seed, CardClass.WARRIOR);
        replay.Player1.PlayHistory.Add(new PlayHistoryEntry(Cards.FromId(id)));
        PoolPilotProof.Resume(replay, next => {
            Core2026Proof.Play(next, next.Player1, "GIL_598");
            Core2026Proof.Check(next.Player1.Choice == null && !next.Resolution.HasSuspendedEffect && next.SpellCompletion == null,
                id + " Tess replay finishes its recipe synchronously");
            Core2026Proof.Check(next.Player1.GraveyardZone.Count(c => c.Card.Id == id) == 1,
                id + " Tess replay completes cast once");
            var kept = next.Player1.HandZone.Single();
            int cost = kept.Card.Id == "JAIL_514" ? 0 : 1;
            Core2026Proof.Check(kept.Card.Type == CardType.SPELL && kept.Card.Cost >= 8 && kept.Cost == cost && kept[GameTag.COST] == cost,
                "Tess Relic grants selected expensive spell with discount");
            Core2026Proof.End(next);
        });

        DecisionSpellOrderingProof.Run(g.Seed, id, (next, chosen) =>
            Core2026Proof.Check(chosen.Zone == next.Player1.HandZone && chosen.Cost == (chosen.Card.Id == "JAIL_514" ? 0 : 1),
                "Relic discount survives deferred completion"));

        TinyPools(g, id);
        var source = Start(g, p, id); var offers = Offers(g, p, source, c => c.Type == CardType.SPELL && c.Cost >= 8);
        foreach (int chosen in offers)
        {
            var branch = g.CloneExact(history: true); Answer(branch, chosen);
            var made = branch.Player1.HandZone.Single();
            // The Unseen Atlas keeps its own "(1) less for each card in your hand" on top of the set Cost (1).
            int cost = made.Card.Id == "JAIL_514" ? Math.Max(0, 1 - branch.Player1.HandZone.Count) : 1;
            Core2026Proof.Check(made.Id == chosen && made.Card.Id == g.IdEntityDic[chosen].Card.Id && made.ZonePosition == 0 &&
                made.Controller == branch.Player1 && made.Cost == cost && made[GameTag.COST] == cost && made.Card.Cost >= 8,
                "each any-class offer enters hand with visible 1 Cost; definition unchanged");
        }
        PoolPilotProof.Resume(g, next => Answer(next, offers[0]));
        PoolPilotProof.EmptyHand(p); Empty(g, p, id);
        // Pin the persistence fixture: The Unseen Atlas's own hand-size discount can make random offers cost 0.
        WithPool(g, id, new[] { "TLC_835" }, () => {
            source = Play(g, p, id); Answer(g, Offers(g, p, source, c => c.Id == "TLC_835").Single());
            var discounted = p.HandZone.Single();
            Core2026Proof.Check(discounted.Cost == 1, "singleton any-class pool works");
            End(g); End(g);
            Core2026Proof.Check(discounted.Cost == 1 && discounted[GameTag.COST] == 1 && discounted.Card.Cost == 10,
                "Relic discount persists past both turn ends; definition unchanged");
        });
        PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p, 9);
        source = Play(g, p, id); PoolPilotProof.FillHand(p);
        int chosenBurn = Offers(g, p, source, c => c.Type == CardType.SPELL && c.Cost >= 8)[0]; Answer(g, chosenBurn);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.IdEntityDic[chosenBurn].Zone == p.GraveyardZone && !g.Resolution.HasSuspendedEffect,
            "full hand burns selected spell and completes effect");
    });
}
