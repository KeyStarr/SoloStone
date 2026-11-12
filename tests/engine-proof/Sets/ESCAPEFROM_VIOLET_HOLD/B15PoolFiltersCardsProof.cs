using System.Linq;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class B15PoolFiltersESCAPEFROMVIOLETHOLDProof
{
    [GroupProof("b15-pool-filters-escapefrom-violet-hold-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("JAIL_730", Unpinned_ScytheHeroOnly);
        StandardSetsProof.Case("JAIL_733", Unpinned_VoidscaleDeathrattleOnly);
        StandardSetsProof.Case("JAIL_891", Unpinned_VoidBlastLethal);
        StandardSetsProof.Case("JAIL_732", Unpinned_VoidSoulSharedProgressAndCap);
    }
    private static void Unpinned_ScytheHeroOnly(Game g, Controller p, string id)
    {
        var weapon = Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 0 && p.Hero.Weapon.Durability == 3, "Scythe no on-equip grant and exact durability");
        var minion = Summon(p, "CS2_182"); Attack(g, minion, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == 0, "minion attack cannot grant Soul");
        B15PoolTest.Resume(g, next => Core2026Proof.Check(next.Process(HeroAttackTask.Any(next.Player1, next.Player2.Hero)), "hero attacks"));
        Core2026Proof.Check(p.HandZone.Single().Card.Id == "JAIL_732" && p.Hero.Weapon.Durability == 2, "hero attack grants one Soul");
        End(g); End(g); PoolPilotProof.FillHand(p);
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "hero attacks");
        Core2026Proof.Check(p.HandZone.Count == 10, "full-hand attack grant burns normally");
    }
    private static void Unpinned_VoidscaleDeathrattleOnly(Game g, Controller p, string id)
    {
        var body = (Minion)Play(g, p, id);
        Core2026Proof.Check(body.HasTaunt && p.HandZone.Count == 0, "Voidscale Taunt without Battlecry grant");
        B15PoolTest.Resume(g, next => Kill(next, (Minion)next.IdEntityDic[body.Id]));
        Core2026Proof.Check(p.HandZone.Single().Card.Id == "JAIL_732", "Deathrattle grants one Soul");
        body = (Minion)Play(g, p, id); body.Silence(); Kill(g, body);
        Core2026Proof.Check(p.HandZone.Count == 1, "Silence suppresses death grant");
    }
    private static void Unpinned_VoidBlastLethal(Game g, Controller p, string id)
    {
        var healthy = Summon(p.Opponent, "CS2_182");
        StandardSetsProof.TargetRules(g, p, id, healthy, Summon(p, "CS2_182"), false);
        Play(g, p, id, healthy);
        Core2026Proof.Check(healthy.Damage == 3 && p.HandZone.All(c => c.Card.Id != "JAIL_732"), "nonlethal hit does not grant Soul");
        B15PoolTest.Resume(g, next => Play(next, next.Player1, id, (Minion)next.IdEntityDic[healthy.Id]));
        Core2026Proof.Check(p.HandZone.Count(c => c.Card.Id == "JAIL_732") == 1 && healthy.Zone == p.Opponent.GraveyardZone, "lethal damage grants one Soul");
        var shield = Summon(p.Opponent, "EX1_008"); shield.HasDivineShield = true;
        Play(g, p, id, shield);
        Core2026Proof.Check(shield.Zone == p.Opponent.BoardZone && p.HandZone.Count(c => c.Card.Id == "JAIL_732") == 1, "prevented damage does not grant");
    }
    private static void Unpinned_VoidSoulSharedProgressAndCap(Game g, Controller p, string id)
    {
        var held = Add(p, id); g.AuraUpdate();
        Core2026Proof.Check(held.Cost == 1 && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "fresh Soul printed Cost and initial summon tier");
        for (int tier = 1; tier <= 12; tier++)
        {
            StandardSetsProof.ParkBoard(p);
            int cost = Math.Min(10, tier);
            var candidates = OfflineCardPools.Candidates(g, p.HeroClass, OfflinePoolFilter.Random(type: CardType.MINION, race: Race.DEMON).WithCost(cost), Cards.FromId(id).AssetId);
            var random = g.Random.Clone(); var expected = candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
            Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Count == (expected == null ? 0 : 1)
                && (expected == null || p.BoardZone.Single().Card.Id == expected.Id), "seeded Demon tier " + tier + " capped at ten");
            g.AuraUpdate();
            Core2026Proof.Check(held.Cost == 1 && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == Math.Min(10, tier + 1), "existing held Soul shares controller progress");
        }
        var later = Add(p, id); g.AuraUpdate();
        Core2026Proof.Check(later[GameTag.TAG_SCRIPT_DATA_NUM_1] == 10, "future Soul inherits capped tier");
        StandardSetsProof.ParkBoard(p); LostCityPoolProof.FullBoard(p);
        var before = g.HistoryFacts.Query.Casts().Count(f => f.DbfId == Cards.FromId(id).AssetId && f.Outcome == HistoryCastOutcome.Completed);
        B15PoolTest.Resume(g, next => new SabberStoneCore.EffectSystem.EffectContext(next, next.Player1.Id, next.Player1.Hero.Id)
            .CastSpell(next.IdEntityDic[later.Id].Id, HistoryCastKind.Effect));
        Core2026Proof.Check(g.HistoryFacts.Query.Casts().Count(f => f.DbfId == Cards.FromId(id).AssetId && f.Outcome == HistoryCastOutcome.Completed) == before + 1,
            "full-board effect resolution still advances completed count");
        End(g); var enemySoul = Add(p.Opponent, id); g.AuraUpdate();
        Core2026Proof.Check(enemySoul[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "opponent controller has independent progression");
    }
}
