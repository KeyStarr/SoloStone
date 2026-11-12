using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HijackedSecuritybotProof
{
    /// <summary>Printed Prepare: 1 Mana discounts the card by 2 and locks it for the turn; the lock expires, the discount stays.</summary>
    internal static IPlayable Prepared(Game g, Controller p, string id)
    {
        var source = Add(p, id);
        p.BaseMana = 1; p.UsedMana = 0; p.TemporaryMana = 0;
        int printed = source.Card.Cost;
        Core2026Proof.Check(source.Card[GameTag.PREPARE] == 1 && source[GameTag.PREPARE] == 1
            && p.Options().Any(t => t is PrepareCardTask && t.Source == source), id + " printed Prepare option");
        Core2026Proof.Check(g.Process(PrepareCardTask.Any(p, source)) && p.RemainingMana == 0
            && source.Cost == printed - 2 && source[GameTag.PREPARING] == 1, id + " Prepare discount and lock");
        p.BaseMana = 10;
        Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source == source), id + " locked this turn");
        End(g); End(g);
        Core2026Proof.Check(source[GameTag.PREPARING] == 0 && source.Cost == printed - 2 && source.Zone == p.HandZone, id + " lock expires, discount kept");
        return source;
    }

    internal static void Run() => StandardSetsProof.Case("JAIL_457", (g, p, id) => {
        var card = Cards.FromId(id);
        Core2026Proof.Check(card.IsRace(Race.ELEMENTAL) && card.IsRace(Race.MECHANICAL) && !card.IsRace(Race.BEAST)
            && card[GameTag.BATTLECRY] == 1, "dual Elemental/Mech Battlecry identity");
        var alone = (Minion)Play(g, p, id);
        Core2026Proof.Check(alone.AttackDamage == 3 && alone.Health == 3 && alone.AppliedEnchantments?.Any(e => e.Card.Id == "JAIL_457e") != true,
            "empty board: no self buff");
        var a = Summon(p, "CS2_182"); var b = Summon(p, "CS2_231"); var enemy = Summon(p.Opponent, "CS2_120");
        var prepared = Prepared(g, p, id);
        PlayEntity(g, p, prepared);
        var source = (Minion)prepared;
        Core2026Proof.Check(a.AttackDamage == 5 && a.Health == 6 && b.AttackDamage == 2 && b.Health == 2 && alone.AttackDamage == 4 && alone.Health == 4,
            "other friendly minions +1/+1 including an earlier Securitybot");
        Core2026Proof.Check(new[] { a, b, alone }.All(m => m.AppliedEnchantments.Count(e => e.Card.Id == "JAIL_457e") == 1),
            "one pinned JAIL_457e each");
        Core2026Proof.Check(source.AttackDamage == 3 && source.Health == 3 && enemy.AttackDamage == 2 && enemy.Health == 3,
            "source and enemy minions unbuffed");
        Kill(g, source);
        Core2026Proof.Check(a.AttackDamage == 5 && a.Health == 6 && b.AttackDamage == 2 && b.Health == 2,
            "buffs persist after Securitybot leaves play");
        var clone = g.CloneExact(history: true);
        ((Minion)clone.IdEntityDic[a.Id]).Silence();
        Core2026Proof.Check(a.AttackDamage == 5 && ((Minion)clone.IdEntityDic[a.Id]).AttackDamage == 4, "clone enchantment independent");
        End(g); End(g);
        Core2026Proof.Check(a.AttackDamage == 5 && a.Health == 6, "permanent buff");
        a.Silence();
        Core2026Proof.Check(a.AttackDamage == 4 && a.Health == 5, "Silence removes buff");
    });
}
