using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class DarkriderProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_456", (g, p, id) => {
        var rng = g.Random.CaptureState();
        Play(g, p, id);
        Core2026Proof.Check(p.Choice == null && g.Random.CaptureState().Equals(rng), "no held Dragon gives no Discover or RNG");
        Summon(p, "TIME_056"); Add(p.Opponent, "TIME_056");
        Entity.FromCard(p, Cards.FromId("TIME_056"), zone: p.DeckZone);
        Add(p, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(p.Choice == null && g.Random.CaptureState().Equals(rng), "own board/deck and enemy hand Dragon do not count");
        GiftedDiscoverProof.Exercise(g.Seed, id, new[] { CardClass.WARRIOR },
            OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.DRAGON));
    });
}
