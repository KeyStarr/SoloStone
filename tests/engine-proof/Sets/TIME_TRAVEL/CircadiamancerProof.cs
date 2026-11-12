using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class CircadiamancerProof
{
    // PINNED-RULE-ONLY: NOTES/rules-evidence/tt_evh-time102-circadiamancer-timing.yaml
    // Tier C rulebook/Alarm-o-Bot + pinned TIME_102e: owner start before draw, independent of creator.
    internal static void Run() => StandardSetsProof.Case("TIME_102", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        var generated = (Minion)p.HandZone.Single();
        TimeTravelPoolProof.RandomGenerated(g, source, new[] { generated }, 1);
        Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 2 && source.Card[GameTag.BATTLECRY] == 1 && generated.Card.Cost == 8 && generated.Cost == 8 &&
            generated.AppliedEnchantments.Single(e => e.Card.Id == "TIME_102e").Target == generated,
            "2/2 Battlecry generates printed 8-Cost minion bearing Metaphysical; no immediate discount");
        source.Silence(); Kill(g, source);
        End(g); Core2026Proof.Check(generated.Cost == 8, "opponent start does not reduce Cost");
        PoolPilotProof.Resume(g, End);
        Core2026Proof.Check(generated.Cost == 7 && generated[GameTag.COST] == 7 && source.Zone == p.GraveyardZone,
            "next owner start reduces hand-visible Cost despite creator Silence/death");
        int costAtDraw = -1;
        void ObserveDraw(IEntity drawn) { if (drawn.Controller == p) costAtDraw = generated.Cost; }
        g.TriggerManager.DrawTrigger += ObserveDraw;
        try { End(g); End(g); }
        finally { g.TriggerManager.DrawTrigger -= ObserveDraw; }
        Core2026Proof.Check(generated.Cost == 6 && generated[GameTag.COST] == 6 && costAtDraw == 6,
            "second owner start accumulates discount before actual turn draw");
        CostRules.AddModifier(generated, EffectOperator.SET, 0);
        End(g); End(g); Core2026Proof.Check(generated.Cost == 0 && generated[GameTag.COST] == 0, "Cost never falls below zero");
        var enchantment = generated.AppliedEnchantments.Single(e => e.Card.Id == "TIME_102e");
        Generic.DiscardBlock(p, generated);
        Core2026Proof.Check(generated.Zone == p.GraveyardZone && enchantment.ActivatedTrigger == null, "leaving hand retires recurring trigger");
        int graveyardCost = generated.Cost;
        End(g); End(g); Core2026Proof.Check(generated.Cost == graveyardCost, "discarded minion no longer ticks");
        TimeTravelPoolProof.ClearQuietly(g, p); PoolPilotProof.EmptyHand(p);
        var pending = Add(p, id);
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[pending.Id]));
        TimeTravelPoolProof.RandomGenerated(g, pending, p.HandZone, 1);
        TimeTravelPoolProof.ClearQuietly(g, p); PoolPilotProof.EmptyHand(p);
        foreach (int count in new[] { 0, 1 }) TimeTravelPoolProof.WithPool(g, id, count, () => {
            var body = Play(g, p, id);
            Core2026Proof.Check(p.HandZone.Count == count, "empty/singleton pool generation");
            if (count == 1) TimeTravelPoolProof.RandomGenerated(g, body, p.HandZone, 1);
            TimeTravelPoolProof.ClearQuietly(g, p); PoolPilotProof.EmptyHand(p);
        });
        var fixture = Entity.FromCard(p, Cards.FromId(id));
        PoolPilotProof.FillHand(p, 9); PoolPilotProof.Resolve(g, fixture);
        var last = p.HandZone.Single(c => c[GameTag.DISPLAYED_CREATOR] == fixture.Id);
        TimeTravelPoolProof.RandomGenerated(g, fixture, new[] { last }, 1);
        Core2026Proof.Check(p.HandZone.Count == 10 && last.AppliedEnchantments.Any(e => e.Card.Id == "TIME_102e"), "last hand slot carries discount enchantment");
        int firstNew = g.NextId; long rng = g.Random.DrawCount;
        PoolPilotProof.Resolve(g, fixture);
        var burned = g.IdEntityDic.Values.Single(e => e.Id >= firstNew && e.Card.Type == CardType.MINION);
        var member = g.OfflinePools.Standard.Single(c => c.CardId == burned.Card.Id);
        Core2026Proof.Check(burned.Card.Id != id && burned.Card.AssetId == member.DbfId && member.Type == "MINION" && member.Cost == 8 && burned.Card.Cost == 8 &&
            burned.Card.ATK == member.Attack && burned.Card.Health == member.Health && burned[GameTag.DISPLAYED_CREATOR] == fixture.Id,
            "burned result retains pinned pool identity, printed Cost/stats and creator");
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount == rng + 1 && burned.Zone == p.GraveyardZone &&
            !(burned.AppliedEnchantments?.Any(e => e.Card.Id == "TIME_102e") ?? false), "full hand burns sampled minion without recurring trigger");
    });
}
