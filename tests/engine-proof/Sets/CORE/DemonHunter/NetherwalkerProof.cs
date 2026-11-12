using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class NetherwalkerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BT_321", (g, p, id) => {
        DecisionChoiceReplayProof.Battlecry(g.Seed, id, CardClass.DEMONHUNTER);
        p.AddHeroAndPower(Cards.FromId("HERO_10"));
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), Cards.FromId(id).AssetId);
        Core2026Proof.Check(candidates.Count >= 3 && candidates.All(c => c.Type == CardType.MINION && c.IsRace(Race.DEMON) && c.Id != id), "admitted filter and source exclusion");
        var clone = g.CloneExact(history: true);
        var source = Play(g, p, id);
        Play(clone, clone.Player1, id);
        var offers = p.Choice.Choices.ToArray();
        Core2026Proof.Check(offers.Length == 3 && offers.Select(x => g.IdEntityDic[x].Card.Id).Distinct().Count() == 3
            && offers.All(x => candidates.Any(c => c.Id == g.IdEntityDic[x].Card.Id))
            && offers.Select(x => g.IdEntityDic[x].Card.Id).SequenceEqual(clone.Player1.Choice.Choices.Select(x => clone.IdEntityDic[x].Card.Id)), "three distinct eligible offers; deterministic seed");

        int chosen = offers[1];
        Resume(g, next => CoreCostProof.Answer(next, chosen));
        Generated(g, source, p.HandZone, 1);
        Core2026Proof.Check(p.HandZone.Single().Id == chosen, "exact selected entity delivered");
        EmptyHand(p); StandardSetsProof.ClearBoard(g, p); FillHand(p, 9);
        Play(g, p, id); chosen = p.Choice.Choices[0]; FillHand(p);
        Resume(g, next => CoreCostProof.Answer(next, chosen));
        Core2026Proof.Check(p.HandZone.Count == 10 && p.Choice == null && g.IdEntityDic[chosen].Zone != p.HandZone, "full hand never exceeds capacity");
        EmptyHand(p); StandardSetsProof.ClearBoard(g, p);
        Pool(g, id, 0, () => {
            long draws = g.Random.DrawCount; Play(g, p, id);
            Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 0 && g.Random.DrawCount == draws, "empty pool no choice or RNG");
        });
    });
}
