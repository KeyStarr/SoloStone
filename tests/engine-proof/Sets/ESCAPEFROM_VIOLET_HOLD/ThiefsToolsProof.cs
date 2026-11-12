using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using static VioletHoldPoolProof;

internal static class ThiefsToolsProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_706", (g, p, id) => {
        // Cost/overflow fixture uses named nonsplitting spells; ShatterRulesProof covers generated splits.
        string[] fixture = { "CORE_CS2_076", "CORE_WON_337" };
        HandLimits(g, p, id, 2, reduction: 2);
        var branch = g.CloneExact();
        string[] expected = null;
        LostCityPoolProof.WithPool(branch, id, fixture, () => expected = Enumerable.Range(0, 2)
            .Select(_ => OfflineCardPools.Pick(branch, VioletHoldPoolProof.Filter(id), p.HeroClass, Cards.FromId(id).AssetId).Id).ToArray());
        IPlayable source = null;
        Resume(g, next => LostCityPoolProof.WithPool(next, id, fixture, () => source = Play(next, next.Player1, id)));
        var results = HandResults(g, source, 2, reduction: 2);
        Core2026Proof.Check(results.Select(c => c.Card.Id).SequenceEqual(expected), "seeded independent spell picks");
        Resume(g, next => Core2026Proof.Check(results.All(c => next.IdEntityDic[c.Id].Cost == 2), "discount survives clone and snapshot"));
        var apprentice = Summon(p, "EX1_608");
        Core2026Proof.Check(results.All(c => c.Cost == 1), "discount stacks with spell-cost aura");
        apprentice.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(results.All(c => c.Cost == 2 && c.Card.Cost == 4), "shared spell costs unchanged");
        ClearBoard(g, p); EmptyHand(p); LocationProofs.Location(p);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        LostCityPoolProof.WithPool(g, id, fixture, () => source = Play(g, p, id));
        HandResults(g, source, 2, reduction: 2);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7, "full board does not block hand generation");
    });
}
