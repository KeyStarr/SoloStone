using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class TendingDragonkinProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_960", (g, p, id) => {

        Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == 0, "no Beast safe");
        Add(p, "CS2_168"); var beast = (Playable)Add(p, "EDR_978");
        var other = Add(p, "CS2_172"); Effects.SetCost(1).ApplyTo(beast);
        new Effect(GameTag.COST, EffectOperator.ADD, 1).ApplyAuraTo(beast);
        CostRules.SetCostThisTurn(beast, 0); Visible(beast, 1);
        Play(g, p, id); var copy = p.HandZone.Last();
        Core2026Proof.Check(copy.Card.Id == "EDR_978" && copy.Id != beast.Id, "copies lowest effective Beast, ignores cheaper non-Beast");
        Visible(copy, 0); End(g); End(g); Visible(copy, 1); Visible(beast, 2);
        StandardSetsProof.ClearBoard(g, p); EmptyHand(p);
        var a = Add(p, "CS2_172"); var b = Add(p, "EDR_978");
        Effects.SetCost(1).ApplyTo(a); Effects.SetCost(1).ApplyTo(b);
        var expected = g.CloneExact(true);
        string chosenId = new[] { a.Card.Id, b.Card.Id }[expected.Random.Next(2)];
        var source = Add(p, id); PlayEntity(g, p, source);
        Core2026Proof.Check(p.HandZone.Last().Card.Id == chosenId
            && g.Random.CaptureState().Equals(expected.Random.CaptureState()), "Beast tie uses deterministic game RNG");
        FillHand(p, 10); int burns = p.GraveyardZone.Count;
        Cards.FromId(id).Power.PowerTask.Process(g, p, source, null);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.GraveyardZone.Count == burns + 1, "full hand burns Beast copy");
    });
}
