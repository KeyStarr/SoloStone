using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static LostCityPoolProof;

internal static class MechacycleProof
{
    // auto-cast/mechacycle-mana-budget.yaml and mechacycle-zero-cost.yaml:
    // STILL-UNPINNED owner assumptions, C wiki/D export/E reveal; budget 4, printed cost, zero allowed, capacity stop.
    internal static void Run() => StandardSetsProof.Case("BE_900", Unpinned_Mechacycle);

    private static void Unpinned_Mechacycle(Game g, Controller p, string id)
    {
        p.AddHeroAndPower(Cards.FromId("HERO_05"));
        var declaration = OfflinePoolDeclarations.Cards.Single(c => c.CardId == id);
        OfflinePoolAdmission.Evaluate(g.OfflinePools, new[] { declaration }).ValidateRegistered();
        var filter = Filter(id);
        Core2026Proof.Check(filter.MinCost == null && filter.MaxCost == null && filter.Race == Race.MECHANICAL
            && declaration.Pools.Single().CostBinding == SabberStoneCore.Tasks.SimpleTasks.PoolCostBinding.StackNumberMaxCost,
            "declared dynamic maximum-cost Mech filter includes zero");
        Core2026Proof.Check(OfflineCardPools.Candidates(g, p.HeroClass, filter.WithMaxCost(4), Cards.FromId(id).AssetId).Count >= 3,
            "Hunter ordinary budget has at least three admitted candidates");
        var source = Summon(p, id);
        Core2026Proof.Check(source.HasTaunt && BEPoolProof.Children(p, source).Length == 0, "printed Taunt; no generation before death");
        PoolPilotProof.Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        var children = BEPoolProof.Children(p, source);
        int remainder = 4;
        Core2026Proof.Check(children.Length > 0 && children.Length <= 7, "death summons Mechs within board capacity");
        foreach (var child in children)
        {
            Core2026Proof.Check(child.Card.IsRace(Race.MECHANICAL) && child.Card.Cost <= remainder
                && child.Card.Id != id && child.AttackDamage == child.Card.ATK && child.Health == child.Card.Health,
                "each generated identity/stats meets remaining printed-cost budget");
            remainder -= child.Card.Cost;
        }
        Core2026Proof.Check(remainder == 0 || p.BoardZone.IsFull
            || OfflineCardPools.Candidates(g, p.HeroClass, filter.WithMaxCost(remainder), source.Card.AssetId).Count == 0,
            "stops only at spent budget, capacity or an empty remaining bucket");

        ParkBoard(p);
        WithPool(g, id, new[] { "CORE_GVG_085" }, () => {
            Minion dying = null;
            var board = DeathrattleSlotBoard(p, () => dying = Summon(p, id));
            Kill(g, dying);
            var made = BEPoolProof.Children(p, dying);
            Core2026Proof.Check(made.Length == 2 && made[0].Id < made[1].Id && made.All(m => m.Card.Id == "CORE_GVG_085" && m.Card.Cost == 2
                && m.AttackDamage == 1 && m.Health == 2 && m.HasTaunt && m.HasDivineShield),
                "singleton two-cost Mech consumes 2+2, with exact Annoy-o-Tron stats/keywords");
            CheckDeathrattleSlot(p, board, made, "Mechs replace dying physical slot in creation order");
        });

        ParkBoard(p);
        WithPool(g, id, new[] { "CORE_DRG_256" }, () => {
            source = Summon(p, id); Kill(g, source);
            Core2026Proof.Check(BEPoolProof.Children(p, source).Single().Card.Id == "CORE_DRG_256", "exact four-cost roll stops after one summon");
        });
        ParkBoard(p);
        WithPool(g, id, new[] { "TIME_060" }, () => {
            source = Summon(p, id); long draws = g.Random.DrawCount; Kill(g, source);
            Core2026Proof.Check(BEPoolProof.Children(p, source).Single().Card.Id == "TIME_060" && g.Random.DrawCount == draws + 1,
                "three-cost roll leaves one unusable budget; empty next bucket stops without extra RNG");
        });
        ParkBoard(p);
        WithPool(g, id, Array.Empty<string>(), () => {
            source = Summon(p, id); var rng = g.Random.CaptureState(); Kill(g, source);
            Core2026Proof.Check(p.BoardZone.IsEmpty && rng.Equals(g.Random.CaptureState()), "empty pool: death only, no RNG");
        });

        // Only one initial slot: no further rolls when the first summon fills it, despite residual budget.
        ParkBoard(p); BEPoolProof.Location(p);
        while (p.BoardZone.FreeSpace > 1) Summon(p, "CS2_182");
        WithPool(g, id, new[] { "CORE_GVG_085" }, () => {
            source = Summon(p, id); long draws = g.Random.DrawCount; Kill(g, source);
            Core2026Proof.Check(p.BoardZone.SlotCount == 7 && BEPoolProof.Children(p, source).Length == 1 && g.Random.DrawCount == draws + 1,
                "mixed full board: one Mech, one roll, no retry");
            draws = g.Random.DrawCount; PoolPilotProof.Resolve(g, source, death: true);
            Core2026Proof.Check(p.BoardZone.SlotCount == 7 && g.Random.DrawCount == draws, "full board: no roll or overflow");
        });
        ParkBoard(p); ParkBoard(p.Opponent);
        WithPool(g, id, new[] { "TIME_603" }, () => {
            Minion dying = null;
            var board = DeathrattleSlotBoard(p, () => dying = Summon(p, id));
            var juggler = Summon(p, "NEW1_019"); juggler.Health = 5;
            var imp = Summon(p.Opponent, "JAIL_007"); imp.Damage = 5;
            g.SetRandomSeed(2); Kill(g, dying);
            var made = BEPoolProof.Children(p, dying);
            Core2026Proof.Check(imp.Zone == p.Opponent.GraveyardZone && made.Length == 1,
                "summon checkpoint fixture: first Timebomb kills wounded Imp via Juggler and dies to its Deathrattle");
            Core2026Proof.Check(p.BoardZone.Slots.Select(e => e.Id).SequenceEqual(new[] { board.Location.Id, board.Left.Id, made[0].Id, board.Right.Id, juggler.Id }),
                "a summon dying between rolls leaves its slot available for the next Mech");
        });
        ParkBoard(p); ParkBoard(p.Opponent); source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.IsEmpty, "Silence cancels the Deathrattle");
        source = Summon(p, id);
        p.SetasideZone.Add(p.BoardZone.Remove(source)); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.IsEmpty && BEPoolProof.Children(p, source).Length == 0, "nondeath source departure summons nothing");
    }
}
