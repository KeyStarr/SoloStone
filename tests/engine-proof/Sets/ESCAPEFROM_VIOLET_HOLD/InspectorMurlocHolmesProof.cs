using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static DiscoverRealProof;

internal static class InspectorMurlocHolmesProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_851", Unpinned_NameWatch);
    private static void Unpinned_NameWatch(Game original, Controller ignored, string id)
    {
        foreach (bool match in new[] { true, false })
        for (int count = 0; count <= 5; count++)
        {
            var g = New(original.Seed); var p = g.Player1;
            for (int i = 0; i < count; i++) Add(p.Opponent, "CS2_029");
            var source = (Minion)Play(g, p, id);
            var offers = Offers(g);
            Assert(offers.Length == Math.Min(4, count), "Holmes offers up to four entities including duplicates");
            if (count == 0) continue;
            Saved(g, next => {
                Answer(next, offers[0].Id); Kill(next, (Minion)next.IdEntityDic[source.Id]);
                End(next); var enemy = next.Player2;
                Play(next, enemy, match ? "CORE_CS2_029" : "CS2_182", match ? next.Player1.Hero : null);
                Assert(next.Player1.HandZone.Count(c => c.Card.Id == "GAME_005") == (match ? 3 : 0),
                    "watch survives Holmes and matches name across different card IDs");
                if (match) Play(next, enemy, "CS2_029", next.Player1.Hero);
                Assert(next.Player1.HandZone.Count(c => c.Card.Id == "GAME_005") == (match ? 3 : 0), "watch pays once");
                End(next); End(next);
                Play(next, enemy, "CS2_029", next.Player1.Hero);
                Assert(next.Player1.HandZone.Count(c => c.Card.Id == "GAME_005") == (match ? 3 : 0), "unused watch expires after next enemy turn");
            });
        }
    }
}
