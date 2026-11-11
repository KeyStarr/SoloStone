using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static CostSliceProof;

internal static class AnvilOfReshapingProof
{
    internal static void Run() => StandardSetsProof.Case("BE_042", (g, p, id) => {
        Definition("BE_042e");
        var card = Add(p, id); Visible(card, 2, CostPaymentKind.Armor);
        p.Hero.Armor = 1;
        Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source == card), "Options excludes insufficient Armor despite Mana");
        Core2026Proof.Check(!card.IsPlayableByPlayer && !g.Process(PlayCardTask.Any(p, card))
            && p.Hero.Armor == 1 && p.Hero.AttackDamage == 0 && p.RemainingMana == 10, "insufficient Armor is atomic despite Mana/Health");
        p.Hero.Armor = 2; p.BaseMana = 0;
        var option = p.Options().Single(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source == card);
        int damageEvents = 0; SabberStoneCore.Model.TriggerManager.TriggerHandler listener = _ => damageEvents++;
        g.TriggerManager.DealDamageTrigger += listener;
        p.Hero.IsImmune = true; p.Hero.HasDivineShield = true;
        Core2026Proof.Check(g.Process(option) && p.Hero.Armor == 0 && p.Hero.Health == 30
            && p.UsedMana == 0 && p.Hero.AttackDamage == 4 && damageEvents == 0, "exact Armor payment; +4 Attack; no damage event");
        Core2026Proof.Check(p.Hero.AppliedEnchantments.Any(e => e.Card.Id == "BE_042e"), "verified Attack actor");
        g.TriggerManager.DealDamageTrigger -= listener;
        PoolPilotProof.Resume(g, End);
        Core2026Proof.Check(p.Hero.AttackDamage == 0, "Attack expires at turn end"); End(g);
        card = Add(p, id); Effects.ReduceCost(1).ApplyTo(card); Visible(card, 1, CostPaymentKind.Armor);
        p.Hero.Armor = 1; p.BaseMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, card)) && p.Hero.Armor == 0 && card[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] == 1, "discount changes Armor amount");
        card = Add(p, id); Effects.SetCost(0).ApplyTo(card); Visible(card, 0, CostPaymentKind.Armor);
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, card)) && card[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] == 0 && p.Hero.AttackDamage == 8, "zero Armor cost; buffs stack");
        End(g); Core2026Proof.Check(p.Hero.AttackDamage == 0, "both Attack buffs expire");
    });
}
