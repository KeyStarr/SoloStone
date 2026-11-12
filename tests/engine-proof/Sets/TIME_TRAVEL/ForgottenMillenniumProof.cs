using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class ForgottenMillenniumProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_615", (g, p, id) => {
        Definition("TIME_615e");
        Core2026Proof.Check(Cards.FromId("TIME_615e")[GameTag.TAG_ONE_TURN_EFFECT] == 1, "verified this-turn actor");
        var declaration = OfflinePoolDeclarations.Cards.Single(c => c.CardId == id);
        var report = OfflinePoolAdmission.Evaluate(g.OfflinePools, new[] { declaration }); report.ValidateRegistered();
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), Cards.FromId(id).AssetId);
        Core2026Proof.Check(report.Cards.Single().PoolRequirements.Single().Count == candidates.Count && candidates.Count >= 3,
            "P11 admitted Undead after source exclusion");
        Core2026Proof.Check(candidates.All(c => c.IsRace(Race.UNDEAD))
            && candidates.Any(c => c.Class != p.HeroClass && c.Class != CardClass.NEUTRAL), "any-class Undead includes other classes");
        var allManifest = OfflinePoolFixtures.Manifest(OfflinePoolFixtures.Fixture());
        var allMember = allManifest.Standard.Single(m => m.Races.Contains("ALL"));
        Core2026Proof.Check(OfflineCardPools.Candidates(allManifest, p.HeroClass, Filter(id), Cards.FromId(id).AssetId)
            .Any(c => c.AssetId == allMember.DbfId), "P11 filter admits valid ALL-race minion metadata");
        var source = Add(p, id);
        var quiet = HeroDivineShieldProof.NewGame(false); EmptyHand(quiet.Player1);
        var quietSource = Add(quiet.Player1, id); Resolve(quiet, quietSource);
        Core2026Proof.Check(quiet.Player1.HandZone.Count == 10, "History=false fills available slots");
        foreach (var held in quiet.Player1.HandZone.Where(c => c.Id != quietSource.Id)) Visible(held, held.Cost, CostPaymentKind.Health);
        End(quiet);
        foreach (var held in quiet.Player1.HandZone) Visible(held, held.Cost);
        long before = g.Random.DrawCount;
        Resume(g, next => Core2026Proof.Check(next.Process(PlayCardTask.Any(next.Player1, next.IdEntityDic[source.Id])), "play Millennium"));
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount - before == 10, "spell leaves hand before ten independent picks");
        Generated(g, source, p.HandZone, 10);
        foreach (var held in p.HandZone) {
            Visible(held, held.Cost, CostPaymentKind.Health);
            var actor = held.AppliedEnchantments.Single(e => e.Card.Id == "TIME_615e");
            Core2026Proof.Check(actor.Creator.Id == source.Id && actor.Power.Enchant == null, "presentation bound without numeric cost recipe");
        }
        var first = (Playable)p.HandZone.First();
        var copy = Generic.Copy(p.Opponent, p.Hero, first, Zone.HAND);
        Visible(copy, copy.Cost, CostPaymentKind.Health);
        p.Hero.Health = first.Cost; p.Hero.Armor = 100;
        Core2026Proof.Check(!CostRules.CanPay(p, CostRules.Quote(p, first)), "equal Health illegal despite Armor");
        p.Hero.Health = first.Cost + 1;
        Core2026Proof.Check(Generic.PayPhase(p, first) && p.Hero.Health == 1, "generated card pays Health");
        p.Hero.Health = 30;
        Resume(g, End);
        foreach (var held in p.HandZone.Concat(p.Opponent.HandZone)) {
            Visible(held, held.Cost);
            Core2026Proof.Check(!(held.AppliedEnchantments?.Any(e => e.Card.Id == "TIME_615e") ?? false), "expiry retires original and copied actor");
        }
        End(g);
        EmptyHand(p); FillHand(p, 9); before = g.Random.DrawCount;
        Resolve(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount - before == 1, "one slot one pick");
        int entities = g.IdEntityDic.Count; var rng = g.Random.CaptureState(); Resolve(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && entities == g.IdEntityDic.Count && rng.Equals(g.Random.CaptureState()), "full hand creates none and consumes no RNG");
        EmptyHand(p);
        Pool(g, id, 1, () => { Resolve(g, source); Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Select(c => c.Card.Id).Distinct().Count() == 1, "singleton picks allow duplicates"); });
        EmptyHand(p);
        Pool(g, id, 0, () => { rng = g.Random.CaptureState(); entities = g.IdEntityDic.Count; Resolve(g, source);
            Core2026Proof.Check(p.HandZone.Count == 0 && entities == g.IdEntityDic.Count && rng.Equals(g.Random.CaptureState()), "empty pool no cards/RNG"); });
    });
}
