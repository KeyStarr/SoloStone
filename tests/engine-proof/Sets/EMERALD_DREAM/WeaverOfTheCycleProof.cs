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

internal static class WeaverOfTheCycleProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_472", (g, p, id) => {

        int hp = p.Opponent.Hero.Health;
        Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Health == hp, "empty hand no damage and no target");
        var held = Add(p, "CS2_029"); Effects.SetCost(5).ApplyTo(held); Visible(held, 5);
        var source = Add(p, id);
        Core2026Proof.Check(p.Options().Any(t => t.Source?.Id == source.Id && t.Target == p.Opponent.Hero), "effective five enables character targeting");
        PlayEntity(g, p, source, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Health == hp - 3, "five-cost spell enables three damage");
        Effects.SetCost(4).ApplyTo(held); Visible(held, 4);
        Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Health == hp - 3, "four-cost spell disables damage");
        StandardSetsProof.ClearBoard(g, p); EmptyHand(p);
        var expensiveMinion = Add(p, "EDR_978");
        Play(g, p, id); Core2026Proof.Check(p.Opponent.Hero.Health == hp - 3, "expensive minion does not qualify");
        held = Add(p, "CS2_029"); Effects.SetCost(5).ApplyTo(held);
        source = Add(p, id); Effects.SetCost(4).ApplyTo(held);
        Cards.FromId(id).Power.PowerTask.Process(g, p, source, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Health == hp - 3, "condition checked again at Battlecry resolution");
        new Effect(GameTag.COST, EffectOperator.ADD, 1).ApplyAuraTo(held); Visible(held, 5);
        Cards.FromId(id).Power.PowerTask.Process(g, p, source, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Health == hp - 6, "hand aura participates in condition");
    });
}
