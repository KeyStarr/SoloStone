using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using static TimeTravelS2Proof;

internal static class InstantMultiverseProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_014", (g, p, id) => {
        Unpinned_TwelveManaFill(g, p, id);
        EmptyPool(g, p, id);
        ParkBoard(p);
        int overload = p.OverloadOwed;
        Play(g, p, id); RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(p.OverloadOwed == overload + 3, "printed Overload amount is three, not boolean tag value");
        Core2026Proof.Check(p.BoardZone.Count > 0 && p.BoardZone.Sum(m => m.Card.Cost) <= 12, "installed pool respects budget");
        ParkBoard(p);
    });

    private static void Unpinned_TwelveManaFill(Game g, Controller p, string id)
    {
        foreach (var fixture in new[] { ("CORE_EX1_014", 4, 12), ("CORE_EX1_162", 6, 12), ("TIME_872", 1, 8), ("CORE_GIL_531", 7, 0) })
        {
            ParkBoard(p);
            WithCards(g, new[] { fixture.Item1 }, () => {
                long draws = g.Random.DrawCount; Resolve(g, p, id);
                Core2026Proof.Check(p.BoardZone.Count == fixture.Item2 && p.BoardZone.Sum(m => m.Card.Cost) == fixture.Item3
                    && g.Random.DrawCount == draws + fixture.Item2, "Unpinned_TwelveManaFill: exact budget or full board, zero-cost picks terminate");
            });
        }
        ParkBoard(p);
        for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        LocationProofs.Location(p);
        WithCards(g, new[] { "CORE_EX1_014" }, () => {
            long draws = g.Random.DrawCount; Resolve(g, p, id);
            Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "CORE_EX1_014") == 1
                && g.Random.DrawCount == draws + 1, "one physical slot left");
            draws = g.Random.DrawCount; Resolve(g, p, id);
            Core2026Proof.Check(g.Random.DrawCount == draws, "full board no random picks");
        });
        ParkBoard(p);
    }
}
