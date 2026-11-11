using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static EmeraldDreamBatch3Proof;

internal static class DefiledSpearProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_842", (g, p, id) => {
        var weapon = (Weapon)Play(g, p, id); var victim = Summon(p.Opponent, "CS2_182");
        var clone = g.Clone();
        var task = clone.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == clone.Player2.Hero);
        Core2026Proof.Check(clone.Process(task) && ((Minion)clone.IdEntityDic[victim.Id]).Damage == 2 && victim.Damage == 0, "clone secondary damage isolated");
        task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero);
        Core2026Proof.Check(g.Process(task) && victim.Damage == 2 && p.Opponent.Hero.Damage == 2, "hero attack excluded from random pool; only other enemy damaged");
        var ally = Summon(p, "CS2_171"); Attack(g, ally, p.Opponent.Hero);
        Core2026Proof.Check(victim.Damage == 2, "friendly minion attack does not trigger");
        End(g); Play(g, p.Opponent, "CS2_106");
        task = p.Opponent.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Hero);
        Core2026Proof.Check(g.Process(task) && victim.Damage == 2, "opponent hero attack ignored");
        End(g); p.Hero.AttackDamage = 3;
        int damage = p.Opponent.Hero.Damage;
        task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == victim);
        Core2026Proof.Check(g.Process(task) && p.Opponent.Hero.Damage == damage + 5, "secondary hit uses total buffed hero Attack; attacked minion excluded");
        End(g); End(g); damage = p.Opponent.Hero.Damage;
        var shield = Summon(p.Opponent, "CS2_182"); shield.HasDivineShield = true;
        int attack = p.Hero.AttackDamage;
        task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero);
        Core2026Proof.Check(g.Process(task) && !shield.HasDivineShield && shield.Damage == 0 && p.Hero.Weapon == null
            && p.Opponent.Hero.Damage == damage + attack, "last durability triggers before break; secondary shield prevents damage");
        Play(g, p, "CS2_106"); End(g); End(g); shield.Damage = 0;
        task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero);
        Core2026Proof.Check(g.Process(task) && shield.Damage == 0, "removed weapon stops secondary hits");
        ClearBoard(g, p.Opponent); Play(g, p, id); End(g); End(g); damage = p.Opponent.Hero.Damage;
        task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero);
        attack = p.Hero.AttackDamage;
        Core2026Proof.Check(g.Process(task) && p.Opponent.Hero.Damage == damage + attack, "no secondary hit when attacked hero is only enemy");
        ClearBoard(g, p); var redirected = Summon(p, "CS2_182"); redirected.BaseHealth = 20;
        End(g); Play(g, p.Opponent, "EX1_533"); End(g);
        damage = p.Opponent.Hero.Damage; attack = p.Hero.AttackDamage;
        task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero);
        Core2026Proof.Check(g.Process(task) && p.Opponent.SecretZone.Count == 0 && redirected.Damage == attack,
            "Misdirection redirects hero attack into only friendly minion");
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage, "redirected friendly attack causes no secondary damage");
    });
}
