using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class AnimatedMoonwellProof
{
    // Tier C M7/Summoning Stone and Cast spell; owner assumptions: cost-sampling/aftercast-paid-cost.yaml, effect-cast-qualification.yaml.
    internal static void Run() => StandardSetsProof.Case("EDR_254", Unpinned_AnimatedMoonwellCommittedCost);
    private static void Unpinned_AnimatedMoonwellCommittedCost(Game g, Controller p, string id)
    {
        var source = (Minion)Play(g, p, id);
        Play(g, p, "CS2_182"); Core2026Proof.Check(source.AttackDamage == 1, "minion play is not spell cast");
        var spell = (Playable)Add(p, "CS2_029"); CostRules.AddModifier(spell, EffectOperator.SET, 2);
        PlayEntity(g, p, spell, p.Opponent.Hero); Core2026Proof.Check(source.AttackDamage == 3, "committed discounted 2 instead of printed 4");
        spell = (Playable)Add(p, "CS2_029"); CostRules.SetCostThisTurn(spell, 0);
        PlayEntity(g, p, spell, p.Opponent.Hero); Core2026Proof.Check(source.AttackDamage == 3, "committed zero retained");
        spell = (Playable)Add(p, "CS2_029"); CostRules.AddModifier(spell, EffectOperator.SET, 2);
        CostRules.AddPaymentOverride(spell, CostPaymentKind.Health); p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, spell, p.Opponent.Hero)) && source.AttackDamage == 5 && p.UsedMana == 0, "Health payment still samples modified Cost 2");
        var apprentice = Summon(p, "EX1_608");
        spell = (Playable)Add(p, "CS2_022");
        g.AuraUpdate();
        Core2026Proof.Check(spell.Cost == 3, "discount aura active before payment");
        PlayEntity(g, p, spell, apprentice);
        Core2026Proof.Check(spell.Cost == 4 && source.AttackDamage == 8, "removing discount during spell keeps committed 3 instead of later 4");
        var automatic = Entity.FromCard(p, Cards.FromId("CS2_025"));
        automatic[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] = 7;
        new EffectContext(g, p.Id, source.Id).CastSpell(automatic.Id, HistoryCastKind.Effect);
        Core2026Proof.Check(source.AttackDamage == 8, "ordinary effect cast with stale paid Cost does not qualify");
        Resume(g, next => {
            Play(next, next.Player1, "CS2_025");
            Core2026Proof.Check(((Minion)next.IdEntityDic[source.Id]).AttackDamage == 10, "active trigger survives clone and snapshot");
        });
        End(g); Play(g, p.Opponent, "CS2_025"); Core2026Proof.Check(source.AttackDamage == 10, "opponent spell cannot trigger"); End(g);
        source.Silence(); Play(g, p, "CS2_025"); Core2026Proof.Check(source.AttackDamage == 1, "silence clears buff and trigger");
        Kill(g, source); Play(g, p, "CS2_025"); Core2026Proof.Check(source.AttackDamage == 1, "source leaves play without lingering trigger");
    }
}
