using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PaltryFlutterwingProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_058", (g, p, id) => {
        var declaration = OfflinePoolDeclarations.Cards.Single(c => c.CardId == id);
        var filter = declaration.Pools.Single().Filter;
        Core2026Proof.Check(filter.Scope == PoolScope.Standard && filter.ClassMode == PoolClassMode.Any &&
            filter.Type == CardType.MINION && filter.MinCost == 2 && filter.MaxCost == 2 &&
            declaration.Pools.Single().CostBinding == PoolCostBinding.None &&
            declaration.Pools.Single().ClassDomain.SequenceEqual(new[] { "NEUTRAL" }),
            "Unpinned_DormantRandomPool PF2 is Standard Any-class printed-2 minions");
        foreach (string cls in PoolRequirement.PlayableClasses)
            Core2026Proof.Check(OfflineCardPools.Candidates(g, (CardClass)Enum.Parse(typeof(CardClass), cls), filter, 120964).Count >= 3,
                "PF2 at least three admitted candidates for " + cls);
        Minion source = null;
        var board = DeathrattleSlotBoard(p, () => source = PlayAt(g, p, id, 2));
        Core2026Proof.Check(source.Card.AssetId == 120964 && source.AttackDamage == 1 && source.Health == 1 &&
            source.HasDeathrattle && source.Card.IsRace(Race.BEAST), "1/1 Beast Deathrattle");
        PoolPilotProof.FillHand(p, 10);
        bool observedSleeping = false;
        TriggerManager.TriggerHandler observe = e => {
            if (e is Minion m && m[GameTag.DISPLAYED_CREATOR] == source.Id)
                observedSleeping = m.IsDormant && DormantRules.TurnsRemaining(m) == 2 && m.ActivatedTrigger == null && m.OngoingEffect == null;
        };
        g.TriggerManager.AfterSummonTrigger += observe;
        Kill(g, source);
        g.DeathProcessingAndAuraUpdate();
        g.TriggerManager.AfterSummonTrigger -= observe;
        var result = p.BoardZone.Single(m => m.Id != board.Left.Id && m.Id != board.Right.Id);
        CheckDeathrattleSlot(p, board, new[] { result }, "Deathrattle uses physical slot including location");
        TimeTravelPoolProof.RandomGenerated(g, source, new[] { result }, 1);
        Core2026Proof.Check(observedSleeping && result.Card.Cost == 2 && result.IsDormant && p.HandZone.Count == 10,
            "Unpinned_DormantReplace pending sleep precedes summon observers; full hand irrelevant");
        var helper = result.AppliedEnchantments.Single(e => e.Card.Id == "TIME_058e");
        Core2026Proof.Check(helper.Card.AssetId == 120965 && helper.Card.Type == CardType.ENCHANTMENT && helper.Card.Cost == 0 &&
            helper.Card.ATK == 0 && helper.Card.Health == 0 && helper.Card[(GameTag)451] == 2 &&
            helper.Card.Text == "<b>Dormant</b>. Awaken in @ |4(turn, turns)." &&
            helper[GameTag.ATTACHED] == result.Id && helper[(GameTag)451] == 2,
            "Unpinned_DormantCounterProjection exact Consequences attachment");
        result.Silence(); result.TakeDamage(p.Opponent.Hero, 99);
        Core2026Proof.Check(result.IsDormant && !result.IsSilenced && result.Damage == 0 && !p.BoardZone.GetAll().Contains(result),
            "Unpinned_DormantExclusions sleeping generated minion excluded");
        foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()) })
        {
            var sleeper = (Minion)next.IdEntityDic[result.Id];
            End(next); Core2026Proof.Check(DormantRules.TurnsRemaining(sleeper) == 2, "Unpinned_DormantClock opponent start does not tick");
            End(next); Core2026Proof.Check(sleeper.IsDormant && DormantRules.TurnsRemaining(sleeper) == 1, "first future own start");
            End(next); End(next);
            Core2026Proof.Check(!sleeper.IsDormant && sleeper.AppliedEnchantments.All(e => e.Card.Id != "TIME_058e") &&
                sleeper.AttackDamage == result.AttackDamage && sleeper.BaseHealth == result.BaseHealth,
                "Unpinned_DormantLifecycle clone/load wakes on second own start after parent died");
        }
        Core2026Proof.Check(result.IsDormant && DormantRules.TurnsRemaining(result) == 2, "clone independence");
        ParkBoard(p); PoolPilotProof.EmptyHand(p);
        board = DeathrattleSlotBoard(p, () => source = PlayAt(g, p, id, 2));
        new ActivateDeathrattleTask(EntityType.TARGET).Process(g, p, p.Hero, source);
        g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        result = p.BoardZone.Single(m => m.Id != source.Id && m.Id != board.Left.Id && m.Id != board.Right.Id);
        Core2026Proof.Check(p.BoardZone.Slots.Select(m => m.Id).SequenceEqual(new[] {
            board.Location.Id, board.Left.Id, source.Id, result.Id, board.Right.Id }),
            "triggered Deathrattle of living source summons immediately to its right");
        ParkBoard(p);
        source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.SlotCount == 0, "Silence removes Flutterwing deathrattle");
        source = Summon(p, id);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        long draws = g.Random.DrawCount;
        new ActivateDeathrattleTask(EntityType.TARGET).Process(g, p, p.Hero, source);
        g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && g.Random.DrawCount == draws, "full board neither summons nor samples");
        Kill(g, source);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Single(m => m.Card.Id != "CS2_182").IsDormant,
            "death frees exactly one of seven physical slots");
        ParkBoard(p);
        var enemySource = Summon(p.Opponent, id); Kill(g, enemySource);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.SlotCount == 0 && p.Opponent.BoardZone.Single().IsDormant,
            "opponent Flutterwing summons only for its controller");
        ParkBoard(p.Opponent);
        source = Summon(p, id);
        TimeTravelPoolProof.WithPool(g, id, 0, () => {
            var rng = g.Random.CaptureState(); Kill(g, source);
            Core2026Proof.Check(p.BoardZone.SlotCount == 0 && rng.Equals(g.Random.CaptureState()), "empty admitted pool no summon/RNG");
        });
    });
}
