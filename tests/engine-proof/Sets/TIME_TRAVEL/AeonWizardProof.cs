using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static Core2026Proof;

internal static class AeonWizardProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_002", (g, p, id) => {
        foreach (var hero in PoolRequirement.PlayableClasses)
        {
            var game = GiftedDiscoverProof.New(g.Seed, PoolRequirement.HeroClass(hero)); var owner = game.Player1;
            var filter = new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: owner.HeroClass, type: CardType.SPELL);
            var candidates = OfflineCardPools.Candidates(game, owner.HeroClass, filter, 119312);
            var wizard = Play(game, owner, id); RewindTestHelpers.KeepIfOffered(game);
            PoolPilotProof.Generated(game, wizard, owner.HandZone, 2, filter: filter);
            Core2026Proof.Check(owner.HandZone.All(c => c.Card.Type == CardType.SPELL),
                "two controller-class spell roots, including DK/DH; exact Shatter halves; no neutral/other-class leakage");
        }
        Play(g, p, id); RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(p.HandZone.Count == 2, "two independent rolls");
        PoolPilotProof.EmptyHand(p); StandardSetsProof.ClearBoard(g, p);
        while (p.BoardZone.FreeSpace > 1) Summon(p, "CS2_182");
        var source = Add(p, id); while (p.HandZone.Count < 9) Add(p, "CS2_182");
        PlayEntity(g, p, source); RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.BoardZone.IsFull, "last board slot and exactly two free hand slots");
        StandardSetsProof.ClearBoard(g, p); PoolPilotProof.EmptyHand(p);
        source = Add(p, id); PoolPilotProof.FillHand(p); PlayEntity(g, p, source); RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Count(c => c.Card.Type == CardType.SPELL) == 1,
            "only one generated spell fits after full-hand play");
        StandardSetsProof.ClearBoard(g, p); PoolPilotProof.EmptyHand(p);
        source = Add(p, id); p.BaseMana = 10; p.UsedMana = 0;
        RewindSliceProof.WithRewindPool(g, () => {
            var rng = g.Random.CaptureState(); PlayEntity(g, p, source);
            Core2026Proof.Check(p.HandZone.Count == 0 && rng.Equals(g.Random.CaptureState()), "empty class-spell pool has no output or RNG before Keep");
            RewindTestHelpers.KeepIfOffered(g);
        });
    });
}
