using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static DeathKnightC1Proof;
using static PoolPilotProof;

internal static class DeathsAdvanceProof
{
    internal static void Run() => DeathKnightC1Proof.Case("CORE_CATA_009", (g, p, id) => {
        var source = Add(p, id);
        var target = Summon(p.Opponent, "CS2_182");
        var location = LocationProofs.Location(p);
        var options = p.Options().Where(t => t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target?.Id == p.Hero.Id) && options.Any(t => t.Target?.Id == p.Opponent.Hero.Id)
            && options.Any(t => t.Target?.Id == target.Id) && options.All(t => t.Target?.Id != location.Id), "any character, never location");
        Pool(g, id, 0, () => {
            var random = g.Random.CaptureState();
            PlayEntity(g, p, source, target);
            Core2026Proof.Check(target.IsFrozen && p.Choice == null && g.Random.CaptureState().Equals(random), "empty pool preserves Freeze without RNG");
        });
        EmptyHand(p); while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        source = Play(g, p, id, p.Hero);
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), source.Card.AssetId);
        Core2026Proof.Check(p.Hero.IsFrozen && p.Choice.Choices.Count == 3 && p.Choice.Choices.All(x =>
            candidates.Any(c => c.Id == g.IdEntityDic[x].Card.Id && c.Type == CardType.SPELL)), "controller-and-neutral admitted spell Discover after Freeze");
        Answer(g); Generated(g, source, p.HandZone, 1);
        EmptyHand(p); FillHand(p, 9);
        Play(g, p, id, p.Opponent.Hero); FillHand(p);
        Answer(g);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.Choice == null && p.Opponent.Hero.IsFrozen, "full hand answer resolves and Freeze persists");
    });
}
