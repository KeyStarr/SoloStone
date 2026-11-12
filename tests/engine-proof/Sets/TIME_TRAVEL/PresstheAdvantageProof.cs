using SabberStoneCore.Enums;
using static Core2026Proof;

internal static class PresstheAdvantageProof
{
    internal static void Run() => StandardSetsProof.Case("END_007", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var held = Add(p, id);
        var targets = p.Options().Where(t => t.Source?.Id == held.Id).ToArray();
        Core2026Proof.Check(targets.Any(t => t.Target == friendly) && targets.Any(t => t.Target == enemy) && targets.Any(t => t.Target == p.Hero) && targets.Any(t => t.Target == p.Opponent.Hero) && targets.All(t => t.Target != null), "requires any character target");
        int hand = p.HandZone.Count; PlayEntity(g, p, held, enemy);
        Core2026Proof.Check(enemy.Damage == 1 && p.Hero.AttackDamage == 1 && p.Hero.Armor == 1 && p.HandZone.Count == hand, "one damage, hero attack, draw and armor");
        Core2026Proof.Check(p.Hero.AppliedEnchantments.Any(e => e.Card.Id == "END_007e1"), "exact attack enchantment");
        Summon(p, "TIME_856"); Play(g, p, id, friendly);
        Core2026Proof.Check(friendly.Damage == 3 && p.Hero.AttackDamage == 2 && p.Hero.Armor == 2, "spell power affects damage only; hero buffs stack");
        End(g); Core2026Proof.Check(p.Hero.AttackDamage == 0 && p.Hero.Armor == 2, "attack expires, armor persists");
    });
}
