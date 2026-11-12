using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Model.Costs;
using static Core2026Proof;
using static PaladinAurasProof;

internal static class B14F2OathbinderProof
{
    internal static void Run() => StandardSetsProof.Case("BE_146", (g, p, id) => {
        Unpinned_OathbinderAfterCastOrder();
        Unpinned_OathbinderPaidCostSnapshot(g, p, id);
        Unpinned_OathbinderPaymentAndCounter();
        Unpinned_OathbinderSuspendedHolyCast();
    });
    private static void Unpinned_OathbinderAfterCastOrder()
    {
        foreach (bool auraFirst in new[] { false, true })
        foreach (bool suspended in new[] { false, true })
        {
            var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
            if (auraFirst) Install(p, "BE_146");
            Summon(p, "CORE_BAR_878");
            if (!auraFirst) Install(p, "BE_146");
            while (p.BoardZone.Count < (suspended ? 5 : 6)) Summon(p, "CS2_231");
            if (suspended) Play(g, p, "DINO_426");
            foreach (var branch in new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()), g })
            {
                var owner = branch.Player1;
                if (suspended) PlayerDecisionProof.Answer(branch, owner.Choice.Choices.First());
                else Play(branch, owner, "CS2_089", owner.BoardZone.First());
                Core2026Proof.Check(owner.BoardZone.Count == 7
                    && owner.BoardZone.Count(m => m.Card.Id == "BAR_878t") == (auraFirst ? 0 : 1)
                    && owner.BoardZone.Count(m => m.Card.Id == "BE_142t") == (auraFirst ? 1 : 0),
                    $"after-cast final slot follows play order, auraFirst={auraFirst}, suspended={suspended}");
            }
        }
    }

    private static void Unpinned_OathbinderPaidCostSnapshot(Game g, Controller p, string id)
    {
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 0 && Active(p).Length == 1, "installing cast excluded from its new listener");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Single().Card.Id == "BE_142t" && p.BoardZone.Single().AttackDamage == 3,
            "older Aura reacts to second installing cast");
        var initial = p.BoardZone.Single(); initial.Silence();
        Core2026Proof.Check(initial.Card.Id == "BE_142t" && initial.AttackDamage == 1 && initial.Health == 1
            && !initial.HasDivineShield, "Soldier Silence removes realized stats/keywords but keeps exported identity");
        var holy = Add(p, "CS2_087"); Effects.SetCost(1).ApplyTo(holy);
        PlayEntity(g, p, holy, p.BoardZone.First());
        Core2026Proof.Check(p.BoardZone.Count == 3 && p.BoardZone.Count(m => m.Card.Id == "BE_142t" && m.AttackDamage == 1
            && m.Health == 1 && m.Cost == 1 && m.HasDivineShield) == 2, "discounted committed Cost supplies independent fresh Soldiers");
        var zero = Add(p, "CS2_087"); Effects.SetCost(0).ApplyTo(zero);
        int before = p.GraveyardZone.Count;
        PlayEntity(g, p, zero, p.BoardZone.First());
        Core2026Proof.Check(p.BoardZone.Count == 3 && p.GraveyardZone.Count >= before + 2,
            "zero-Cost creates 0/0 Soldiers and ordinary death processing");
        var indirect = (Spell)Entity.FromCard(p, Cards.FromId("CS2_087")); Effects.SetCost(2).ApplyTo(indirect);
        Generic.CastSpell(p, g, indirect, p.BoardZone.First(), 0);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "BE_142t" && m.AttackDamage == 2) == 2,
            "effect cast uses effective invocation Cost despite no resource payment");
        int own = p.BoardZone.Count;
        Generic.CastSpell(p.Opponent, g, (Spell)Entity.FromCard(p.Opponent, Cards.FromId("CS2_089")), p.Opponent.Hero, 0);
        Core2026Proof.Check(p.BoardZone.Count == own, "opponent Holy cast cannot trigger friendly Aura");
    }

    private static void Unpinned_OathbinderPaymentAndCounter()
    {
        foreach (var kind in new[] { CostPaymentKind.Health, CostPaymentKind.Armor })
        {
            var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
            Install(p, "BE_146"); p.Hero.Armor = 10;
            var holy = (Playable)Add(p, "CS2_089"); Effects.SetCost(1).ApplyTo(holy);
            CostRules.AddPaymentOverride(holy, kind);
            Core2026Proof.Check(g.Process(PlayCardTask.SpellTarget(p, holy, p.Hero)), "alternate-resource Holy play");
            Core2026Proof.Check(p.BoardZone.Single().AttackDamage == 1 && p.BoardZone.Single().Health == 1,
                "Health/Armor payment retains numeric committed quote");
            p.BoardZone.Single().Silence();
            Core2026Proof.Check(!p.BoardZone.Single().HasDivineShield, "Soldier Silence removes shield");
        }
        var countered = HeroDivineShieldProof.NewGame(true); var owner = countered.Player1;
        var secret = (Spell)Entity.FromCard(owner.Opponent, Cards.FromId("EX1_287"), zone: owner.Opponent.SecretZone);
        secret.Power.Trigger.Activate(secret);
        Play(countered, owner, "BE_146");
        Core2026Proof.Check(Active(owner).Length == 0 && owner.BoardZone.Count == 0, "countered installing cast creates no actor or Soldier");
    }

    private static void Unpinned_OathbinderSuspendedHolyCast()
    {
        var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
        Install(p, "BE_146");
        Play(g, p, "DINO_426");
        Core2026Proof.Check(p.Choice != null && p.BoardZone.Count == 0, "unfinished Holy Discover cannot summon a Soldier yet");
        foreach (var branch in new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()), g })
        {
            var owner = branch.Player1;
            PlayerDecisionProof.Answer(branch, owner.Choice.Choices.First());
            var soldier = owner.BoardZone.Single(m => m.Card.Id == "BE_142t");
            Core2026Proof.Check(soldier.AttackDamage == 2 && soldier.Health == 2 && owner.BoardZone.Count == 2
                && owner.Choice == null, "resumed Holy cast delivers exactly one saved-cost notification after its summon");
        }
    }
}
