using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class WinterspringWhelpProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_484", (g, p, id) => {
        DecisionChoiceReplayProof.Battlecry(g.Seed, id, CardClass.MAGE);
        SourceExcluded(g, id);
        var source = Summon(p, id);
        Pool(g, id, 0, () => {
            var random = g.Random.CaptureState(); int entities = g.IdEntityDic.Count;
            Resolve(g, source);
            Core2026Proof.Check(p.Choice == null && entities == g.IdEntityDic.Count && g.Random.CaptureState().Equals(random), "empty Discover has no choice/entities/RNG");
        });
        FillHand(p); Resolve(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.Choice == null && !g.Resolution.HasSuspendedEffect,
            "indirect full-hand Discover resolves without overflow");
        EmptyHand(p);
        source = (Minion)Play(g, p, id);
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), source.Card.AssetId);
        Core2026Proof.Check(p.Choice.Choices.All(x => candidates.Any(c => c.Id == g.IdEntityDic[x].Card.Id)), "any-class one-cost spell offers");
        int chosen = p.Choice.Choices[1];
        Resume(g, next => CoreCostProof.Answer(next, chosen));
        Generated(g, source, p.HandZone, 1);
    });
}
