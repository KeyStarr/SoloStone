using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static DiscoverRealProof;

internal static class AncientAugurProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_303", Unpinned_ExactHandIdentity);
    private static void Unpinned_ExactHandIdentity(Game original, Controller ignored, string id)
    {
        foreach (string mode in new[] { "normal", "left", "returned", "silence", "bounce" })
        for (int count = 0; count <= 4; count++)
        {
            var g = New(original.Seed); var p = g.Player1;
            for (int i = 0; i < count; i++) Add(p.Opponent, "CS2_182");
            var source = (Minion)Play(g, p, id);
            var offers = Offers(g);
            Assert(offers.Length == Math.Min(3, count), "Augur samples hand entities, retaining duplicate definitions");
            if (count == 0) { Kill(g, source); Assert(p.Opponent.HandZone.Count == 0, "empty hand no saved discard"); continue; }
            int bound = Bound(offers[0]);
            Saved(g, next => {
                var augur = (Minion)next.IdEntityDic[source.Id]; var enemy = next.Player2; var chosen = next.IdEntityDic[bound];
                Answer(next, offers[0].Id);
                if (mode == "left" || mode == "returned")
                {
                    enemy.SetasideZone.Add(enemy.HandZone.Remove(chosen));
                    if (mode == "returned") Generic.AddHandPhase(enemy, enemy.SetasideZone.Remove(chosen));
                }
                if (mode == "silence") augur.Silence();
                if (mode == "bounce") Generic.ReturnToHandBlock(next.Player1, augur);
                else Kill(next, augur);
                Assert(chosen.Zone == enemy.GraveyardZone == (mode == "normal"), "only original resident entity discarded, no same-name retarget");
                Assert(enemy.HandZone.Count == count - (mode == "normal" || mode == "left" ? 1 : 0), "exact enemy hand count");
            });
        }
        var replay = New(original.Seed); var owner = replay.Player1;
        Add(owner.Opponent, "CS2_182"); Add(owner.Opponent, "CS2_200");
        var augurReplay = (Minion)Play(replay, owner, id);
        var first = Offers(replay)[0]; int oldTarget = Bound(first);
        Answer(replay, first.Id); Generic.ReturnToHandBlock(owner, augurReplay);
        PlayEntity(replay, owner, augurReplay);
        var second = Offers(replay).First(o => Bound(o) != oldTarget); int newTarget = Bound(second);
        Answer(replay, second.Id); Kill(replay, augurReplay);
        Assert(replay.IdEntityDic[oldTarget].Zone == owner.Opponent.HandZone
            && replay.IdEntityDic[newTarget].Zone == owner.Opponent.GraveyardZone, "bounce/replay clears old selected association");
    }
}
