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

internal static class DreamboundDiscipleProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_847", (g, p, id) => {

        Definition("EDR_847e");
        var source = (Minion)Play(g, p, id); Visible(p.Hero.HeroPower, 0);
        Core2026Proof.Check(g.CostState.Rules.Single().EnchantmentId != 0, "controller presentation bound");
        source.Silence(); g.AuraUpdate(); Visible(p.Hero.HeroPower, 0);
        End(g); End(g); Visible(p.Hero.HeroPower, 0);
        p.Hero.HeroPower = (HeroPower)Entity.FromCard(p, Cards.FromId("CS2_034"));
        Visible(p.Hero.HeroPower, 0);
        foreach (var restored in new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()) }) {
            var actor = restored.Player1; actor.BaseMana = 0; actor.UsedMana = 0;
            Core2026Proof.Check(Generic.HeroPower(actor, actor.Opponent.Hero), "free replacement power works after restore");
            Visible(actor.Hero.HeroPower, 2); Visible(p.Hero.HeroPower, 0);
            Core2026Proof.Check(restored.CostState.Rules.Count == 0, "restored one-use independent");
        }
        p.BaseMana = 0; p.UsedMana = 0;
        Core2026Proof.Check(Generic.HeroPower(p, p.Opponent.Hero), "activation consumes at zero Mana");
        Visible(p.Hero.HeroPower, 2);
        var dead = Summon(p, id); Kill(g, dead); Visible(p.Hero.HeroPower, 0);
        p.Hero.HeroPower.IsExhausted = true;
        Core2026Proof.Check(!Generic.HeroPower(p, p.Opponent.Hero) && g.CostState.Rules.Count == 1, "failed activation retains rule");
        p.Hero.HeroPower.IsExhausted = false;
        Core2026Proof.Check(Generic.HeroPower(p, p.Opponent.Hero), "Deathrattle grants next activation");
        Visible(p.Hero.HeroPower, 2);
        var silenced = Summon(p, id); silenced.Silence(); Kill(g, silenced);
        Core2026Proof.Check(g.CostState.Rules.Count == 0, "Silence suppresses Deathrattle");
    });
}
