using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class BloodDrawProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_612", (g, p, id) => {
        DecisionChoiceReplayProof.Spell(g.Seed, id);
        // HERO_11 is unavailable: pin the DK pool, then exercise off-class acquisition with the real Rogue hero.
        var declaration = OfflinePoolDeclarations.Cards.Single(c => c.CardId == id);
        var report = OfflinePoolAdmission.Evaluate(g.OfflinePools, new[] { declaration }); report.ValidateRegistered();
        Core2026Proof.Check(report.Cards.Single().PoolRequirements.Single().Count >= 3, "admitted DK/neutral spells");
        var source = Add(p, id); Visible(source, 3, CostPaymentKind.Health);
        var dkCandidates = OfflineCardPools.Candidates(g, CardClass.DEATHKNIGHT, Filter(id), source.Card.AssetId);
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), source.Card.AssetId);
        Core2026Proof.Check(dkCandidates.Select(c => c.Id).ToHashSet()
            .SetEquals(OfflinePoolExpectations.Ids(Filter(id),"DEATHKNIGHT",source.Card.AssetId)), "admitted DK identities match eligibility/export");
        foreach (int health in new[] { 2, 3 }) {
            p.Hero.Health = health; p.Hero.Armor = 20;
            Core2026Proof.Check(!p.Options().Any(t => t.Source == source) && !g.Process(PlayCardTask.Any(p, source)) && source.Zone == p.HandZone && p.Hero.Health == health, "equal/insufficient Health rejected atomically");
        }
        Effects.ReduceCost(1).ApplyTo(source); Visible(source, 2, CostPaymentKind.Health);
        p.Hero.Health = 3; p.BaseMana = 0; p.Hero.IsImmune = true; p.Hero.HasDivineShield = true;
        int events = 0; TriggerManager.TriggerHandler listener = _ => events++; g.TriggerManager.DealDamageTrigger += listener;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source)), "Health spell accepted with zero Mana");
        Core2026Proof.Check(p.Hero.Health == 1 && p.Hero.Armor == 20 && p.RemainingMana == 0 && events == 0 && p.Hero.HasDivineShield, "discounted Health payment bypasses damage/prevention");
        g.TriggerManager.DealDamageTrigger -= listener;
        Core2026Proof.Check(p.Choice.Choices.Count == 3 && p.Choice.Choices.Select(c => g.IdEntityDic[c].Card.Id).Distinct().Count() == 3 && p.Choice.Choices.All(c => candidates.Any(x => x.Id == g.IdEntityDic[c].Card.Id)), "three distinct controller-class spell offers");
        int chosen = p.Choice.Choices.First(); Resume(g, next => CoreCostProof.Answer(next, chosen));
        Generated(g, source, p.HandZone, 1);
        EmptyHand(p); FillHand(p); var before = p.GraveyardZone.Count;
        Resolve(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.GraveyardZone.Count == before + 1, "full hand burns selected spell");
        EmptyHand(p);
        Pool(g, id, 0, () => { var rng = g.Random.CaptureState(); Resolve(g, source); Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 0 && rng.Equals(g.Random.CaptureState()), "empty pool no choice or RNG"); });
        Pool(g, id, 1, () => { Resolve(g, source); Core2026Proof.Check(p.Choice == null && p.HandZone.Count == 1 && !g.Resolution.HasSuspendedEffect, "singleton indirect Discover completes"); });
    });
}
