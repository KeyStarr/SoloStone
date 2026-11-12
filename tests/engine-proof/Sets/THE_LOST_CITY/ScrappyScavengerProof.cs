using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class ScrappyScavengerProof
{
    // cost-sampling/scavenger-empty-pool.yaml: PINNED (C Notes + independent E observations).
    // scavenger-postpayment.yaml: STILL-UNPINNED, C six-to-five example; owner assumption: current spendable Mana.
    internal static void Run() => StandardSetsProof.Case("TLC_461", Unpinned_ScrappyScavenger);

    private static void Unpinned_ScrappyScavenger(Game fixture, Controller owner, string id)
    {
        foreach (var sample in new[] { (6, 1, 0, 0, 5), (6, 0, 0, 0, 6), (6, 1, 2, 0, 7), (6, 1, 0, 2, 3), (1, 1, 0, 0, 0) })
        {
            var g = GiftedDiscoverProof.New(fixture.Seed, CardClass.MAGE); var p = g.Player1;
            var source = Add(p, id); source.Cost = sample.Item2;
            p.BaseMana = sample.Item1; p.TemporaryMana = sample.Item3; p.OverloadLocked = sample.Item4;
            var filter = OfflinePoolFilter.Discover().WithCost(sample.Item5);
            var expected = OfflineCardPools.SampleDistinct(g.CloneExact(history: true), filter, p.HeroClass, source.Card.AssetId);
            Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source)), "manual play at exact Mana accepted");
            if (sample.Item5 == 0)
            {
                Core2026Proof.Check(expected.Length == 0 && p.Choice == null && !g.Resolution.HasSuspendedEffect
                    && p.HandZone.Count == 0, "zero-Cost Mage bucket genuinely empty: no substitute or stale cursor");
                continue;
            }
            var offers = p.Choice.Choices.Select(n => g.IdEntityDic[n]).ToArray();
            Core2026Proof.Check(offers.Length == 3 && offers.Select(c => c.Card.Id).SequenceEqual(expected.Select(c => c.Id))
                && offers.Select(c => c.Card.Id).Distinct().Count() == 3
                && offers.All(c => c.Card.Cost == sample.Item5 && c.Card.Id != id
                    && g.OfflinePools.Standard.Single(m => m.CardId == c.Card.Id).Classes.Any(v => v == "MAGE" || v == "NEUTRAL")),
                "Unpinned_Scavenger_Postpayment: seeded distinct Mage/neutral offers at post-payment available Mana");
            PoolPilotProof.Resume(g, branch => {
                PlayerDecisionProof.Answer(branch, offers[0].Id);
                Core2026Proof.Check(branch.IdEntityDic[offers[0].Id].Zone == branch.Player1.HandZone
                    && branch.Player1.Choice == null && !branch.Resolution.HasSuspendedEffect, "selected entity delivered once");
            });
        }
        {
            var g = GiftedDiscoverProof.New(fixture.Seed, CardClass.MAGE); var p = g.Player1;
            var source = Play(g, p, id);
            while (!p.HandZone.IsFull) Add(p, "CS2_182");
            int choice = p.Choice.Choices.First(); PlayerDecisionProof.Answer(g, choice);
            Core2026Proof.Check(p.HandZone.Count == 10 && g.IdEntityDic[choice].Zone == p.GraveyardZone,
                "full hand burns selected Discover card");
        }
    }
}
