using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class StormfuryProof
{
    // Removing spell scaling, the fixed target, hand enrollment or Armor-inclusive Lifesteal fails this.
    internal static void Run() => StandardSetsProof.Case("JAIL_805", (g, p, id) => {
        EmeraldDreamCardStateProof.Token("JAIL_805t", 126414);
        EmeraldDreamCardStateProof.HiddenUpgrade(id, "JAIL_805t");
        var friendly = Summon(p, "CS2_182"); Summon(p, "CS2_052");
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 20;
        var second = Summon(p.Opponent, "CS2_182"); second.BaseHealth = 20;
        p.Hero.Damage = 20;
        Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 3 && second.Damage == 3 && friendly.Damage == 0
            && p.Opponent.Hero.Damage == 0 && p.Hero.Damage == 14, "spell AoE2 plus Spell Damage; heals every enemy minion hit only");
        var upgraded = EmeraldDreamCardStateProof.UpgradeBySpells(g, p, id, "JAIL_805t");
        PlayEntity(g, p, upgraded);
        var elemental = (Minion)upgraded;
        Core2026Proof.Check(enemy.Damage == 5 && second.Damage == 5 && p.Hero.Damage == 10
            && elemental.AttackDamage == 7 && elemental.BaseHealth == 7 && elemental.HasLifeSteal
            && !g.IdEntityDic.Values.Any(e => e.Card.Id == "JAIL_805e"), "fixed 7/7 Lifesteal target, Battlecry2 ignores Spell Damage; Stormpowered excluded");
        p.Hero.Damage = 15; p.Opponent.Hero.Armor = 10;
        Resume(g, next => Attack(next, (Minion)next.IdEntityDic[elemental.Id], next.Player2.Hero));
        Core2026Proof.Check(p.Hero.Damage == 8 && p.Opponent.Hero.Armor == 3 && p.Opponent.Hero.Damage == 0,
            "Lean_LifestealArmorDamage: upgraded Elemental attack heals all seven Armor damage");
        elemental.Silence();
        Core2026Proof.Check(!elemental.HasLifeSteal && elemental.AttackDamage == 7 && elemental.BaseHealth == 7, "Silence removes Lifesteal without changing fixed body");
    });
}
