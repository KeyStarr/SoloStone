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

internal static class WarlocProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_180", (g, p, id) => {

        Definition("CATA_180e");
        var source = (Minion)Play(g, p, id); source.Silence(); Kill(g, source);
        var murloc = Add(p, "CS2_168"); Effects.SetCost(4).ApplyTo(murloc); Visible(murloc, 4);
        var other = Add(p, "CS2_182"); Effects.SetCost(1).ApplyTo(other); Visible(other, 1);
        Effects.SetCost(3).ApplyTo(murloc); Visible(murloc, 3, CostPaymentKind.Health);
        End(g); End(g); Visible(murloc, 3, CostPaymentKind.Health);
        foreach (var restored in new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()) }) {
            var actor = restored.Player1; var card = restored.IdEntityDic[murloc.Id];
            actor.Hero.Health = 4; Core2026Proof.Check(Generic.PayPhase(actor, card) && actor.Hero.Health == 1, "restored rule pays Health");
            Visible(murloc, 3, CostPaymentKind.Health); Visible(card, 3);
        }
        p.BaseMana = 0; p.UsedMana = 0; p.Hero.Health = 3; p.Hero.Armor = 50;
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == murloc.Id) && !g.Process(PlayCardTask.Any(p, murloc)), "equal Health illegal despite Armor");
        Visible(murloc, 3, CostPaymentKind.Health);
        p.Hero.Health = 4; p.Hero.IsImmune = true; p.Hero.HasDivineShield = true;
        int damageEvents = 0; TriggerManager.TriggerHandler observed = _ => damageEvents++;
        p.Hero.TakeDamageTrigger += observed;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, murloc)) && p.Hero.Health == 1 && p.Hero.Armor == 50
            && p.RemainingMana == 0 && p.Hero.HasDivineShield && damageEvents == 0, "Health payment bypasses damage and prevention");
        p.Hero.TakeDamageTrigger -= observed;
        var next = Add(p, "CS2_168"); Visible(next, 1);
        Core2026Proof.Check(g.CostState.Rules.Count == 0 && !next.IsPlayableByPlayer, "committed play consumes rule");
    });
}
