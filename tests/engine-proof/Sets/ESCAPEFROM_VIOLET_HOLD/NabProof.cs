using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Decisions;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class NabProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_225", (g, p, id) => {
        var target = Summon(p.Opponent, "CS2_182"); target.Damage = 2; Effects.Attack_N(5).ApplyTo(target); Effects.SetCost(8).ApplyTo(target);
        CostRules.SetCostThisTurn(target, 1); CostRules.AddPaymentOverride(target, CostPaymentKind.Health, g.Turn);
        Visible(target, 1, CostPaymentKind.Health);
        int count = p.DeckZone.Count;
        Resume(g, other => {
            var spell = Play(other, other.Player1, id, (Minion)other.IdEntityDic[target.Id]);
            var fresh = other.Player1.DeckZone.Single(c => c[GameTag.DISPLAYED_CREATOR] == spell.Id);
            Core2026Proof.Check(fresh.Id != target.Id && fresh.Card.Id == target.Card.Id, "fresh backward-copy identity");
            Visible(fresh, 2);
            Core2026Proof.Check(CostRules.Quote(other.Player1, fresh).Kind == CostPaymentKind.Mana, "backward copy drops Health override before expiry");
        });
        var copy = p.DeckZone.Single(c => c[GameTag.DISPLAYED_CREATOR] != 0);
        Core2026Proof.Check(target.Zone == p.Opponent.GraveyardZone && p.DeckZone.Count == count + 1, "lethal damage shuffles copy");
        Core2026Proof.Check(copy is Minion m && m.AttackDamage == m.Card.ATK && m.Damage == 0 && m.Health == m.Card.Health, "backward copy loses damage and buffs");
        int copyId = copy.Id;
        Resume(g, other => {
            var fresh = other.IdEntityDic[copyId]; Visible(fresh, 2);
            CostRules.ExpireTurn(other, other.Turn); Visible(fresh, 2);
            Core2026Proof.Check(CostRules.Quote(other.Player1, fresh).Kind == CostPaymentKind.Mana, "fresh deck copy remains two Mana after original overlays expire");
            Generic.Draw(other.Player1, fresh); Visible(fresh, 2);
            Core2026Proof.Check(fresh.Zone == other.Player1.HandZone && CostRules.Quote(other.Player1, fresh).Kind == CostPaymentKind.Mana,
                "draw retains two Mana through clone and snapshot");
        });
        var retained = Generic.Copy(p, copy, copy, Zone.HAND); Visible(retained, 2);
        target = Summon(p.Opponent, "CS2_182"); target.BaseHealth = 10; count = p.DeckZone.Count;
        Play(g, p, id, target); Core2026Proof.Check(target.Damage == 3 && p.DeckZone.Count == count, "survivor not copied");
        var friendly = Summon(p, "CS2_182"); StandardSetsProof.TargetRules(g, p, id, target, friendly, false);
        target.HasDivineShield = true; Play(g, p, id, target); Core2026Proof.Check(target.Damage == 3 && p.DeckZone.Count == count, "prevented damage not lethal");
    });
}
