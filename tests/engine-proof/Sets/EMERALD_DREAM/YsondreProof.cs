using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_YsondreProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_465", (g, p, id) => {
        // deathrattle-copy/ysondre-shared-death-count.yaml: owner-approved C/D assumption; all controllers, full current death batch.
        // A reordered mixed board and seeded independent reference reject always-first sampling.
        var filter = SabberStoneCore.CardSets.Client.OfflinePoolDeclarations.Cards.Single(c => c.CardId == id).Pools.Single().Filter;
        var candidates = OfflineCardPools.Candidates(g, CardClass.WARRIOR, filter, Cards.FromId(id).AssetId);
        Core2026Proof.Check(candidates.Count >= 3 && candidates.All(c => c.Type == CardType.MINION && c.IsRace(Race.DRAGON) && c.Id != id), "declared Warrior Dragon pool meets admission floor");
        Console.WriteLine("b12-warrior pool EDR_465 dragon WARRIOR=" + candidates.Count);
        foreach (int size in new[] { 0, 1, 2 })
        {
            var test = g.CloneExact(history: true); var owner = test.Player1;
            var prior = Summon(owner, id); prior.Silence(); Kill(test, prior);
            var body = Summon(owner, id);
            EmeraldDreamPoolProof.WithPool(test, id, size, () => {
                var randomBefore = test.Random.DrawCount; Kill(test, body);
                Core2026Proof.Check(owner.BoardZone.Count == (size == 0 ? 0 : 2), "empty/tiny Dragon generation pool");
                Core2026Proof.Check(test.Random.DrawCount - randomBefore == (size == 0 ? 0 : 2), "independent seeded picks including singleton");
                if (size == 1) Core2026Proof.Check(owner.BoardZone.Select(m => m.Card.Id).Distinct().Count() == 1, "random picks permit duplicate Dragon identities");
            });
        }
        var board = DeathrattleSlotBoard(p, () => PlayAt(g, p, id, 2));
        var source = p.BoardZone.Single(m => m.Card.Id == id); Core2026Proof.Check(source.HasTaunt, "printed Taunt");
        var random = g.Random.Clone(); var expectedFirst = candidates[random.Next(candidates.Count)].Id;
        Kill(g, source);
        var outputs = p.BoardZone.Where(m => m != board.Left && m != board.Right).ToArray();
        Core2026Proof.Check(outputs.Length == 1 && outputs[0].Card.Id == expectedFirst, "current death included"); CheckDeathrattleSlot(p, board, outputs, "random Dragon at death slot");
        foreach (var m in outputs) Core2026Proof.Check(m.IsRace(Race.DRAGON) && m.Card.Id != id && m.AttackDamage == m.Card.ATK && m.Health == m.Card.Health, "admitted Dragon with pinned base stats; source excluded");
        ParkBoard(p); var other = Summon(p.Opponent, id); other.Silence(); Kill(g, other);
        source = Summon(p, id);
        random = g.Random.Clone(); var expectedThree = Enumerable.Range(0, 3).Select(_ => candidates[random.Next(candidates.Count)].Id).ToArray();
        Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Select(m => m.Card.Id).SequenceEqual(expectedThree), "seeded independent picks retain order and allow replacement");
        Core2026Proof.Check(p.BoardZone.Count == 3, "global count includes opponent silenced Ysondre death");
        ParkBoard(p); ParkBoard(p.Opponent);
        source = Summon(p, id); other = Summon(p.Opponent, id);
        source.ToBeDestroyed = true; other.ToBeDestroyed = true; g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count == 5 && p.Opponent.BoardZone.Count == 5, "both Deathrattles see both simultaneous deaths");
        ParkBoard(p); ParkBoard(p.Opponent); LocationProofs.Location(p); source = Summon(p, id);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182"); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.IsRace(Race.DRAGON)) == 1, "random summons stop at physical board cap");
        ParkBoard(p); source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 0, "Silence removes Deathrattle");
        source = Summon(p, id); var clone = g.CloneExact(history: true); Kill(clone, (Minion)clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(clone.Player1.BoardZone.SlotCount == 7 && p.BoardZone.Single() == source, "clone history and random generation isolated");
    });
}
