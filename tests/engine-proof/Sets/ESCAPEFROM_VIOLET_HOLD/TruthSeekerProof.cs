using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TruthSeekerProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_329", (g, p, id) => {
        var paladin = Summon(p, "EX1_362"); var neutral = Summon(p, "CS2_182"); var multi = Summon(p, "CFM_321"); var enemy = Summon(p.Opponent, "EX1_362");
        Play(g, p, id); Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 3 && p.Hero.Weapon.Durability == 3, "printed weapon stats");
        var clone = g.Clone(); var swing = clone.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == clone.Player2.Hero.Id);
        Core2026Proof.Check(clone.Process(swing), "clone hero attack");
        Core2026Proof.Check(((Minion)clone.IdEntityDic[paladin.Id]).AttackDamage == 4 && paladin.AttackDamage == 2, "weapon trigger clone isolated");
        Attack(g, neutral, p.Opponent.Hero); Core2026Proof.Check(paladin.AttackDamage == 2, "minion attack excluded");
        swing = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == p.Opponent.Hero.Id); Core2026Proof.Check(g.Process(swing), "hero attack");
        Core2026Proof.Check(paladin.AttackDamage == 4 && paladin.BaseHealth == 4 && multi.AttackDamage == 3 && neutral.AttackDamage == 4 && enemy.AttackDamage == 2, "friendly Paladin and multiclass only");
        paladin.Silence(); Core2026Proof.Check(paladin.AttackDamage == 2 && paladin.BaseHealth == 2, "Silence removes buff");
        End(g); Play(g, p.Opponent, "CS2_082"); swing = p.Opponent.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == p.Hero.Id); Core2026Proof.Check(g.Process(swing), "opponent swing");
        Core2026Proof.Check(multi.AttackDamage == 3, "opponent hero attack excluded"); End(g); Core2026Proof.Check(multi.AttackDamage == 3, "permanent buff survives end turn");
        p.Hero.Weapon.Damage = 2;
        swing = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == p.Opponent.Hero.Id); Core2026Proof.Check(g.Process(swing), "breaking swing");
        Core2026Proof.Check(p.Hero.Weapon == null && multi.AttackDamage == 5, "last durability still buffs");
        End(g); End(g); Play(g, p, id);
        Play(g, p, "CS2_082"); swing = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target?.Id == p.Opponent.Hero.Id); Core2026Proof.Check(g.Process(swing), "replacement swing");
        Core2026Proof.Check(multi.AttackDamage == 5, "replaced weapon trigger removed");
    });
}
