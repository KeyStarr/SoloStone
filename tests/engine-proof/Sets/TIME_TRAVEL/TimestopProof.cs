using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TimestopProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_611", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var spell = Add(p, id); var options = p.Options().Where(t => t.Source?.Id == spell.Id).ToArray();
        Core2026Proof.Check(new ICharacter[] { friendly, enemy, p.Hero, p.Opponent.Hero }.All(m => options.Any(t => t.Target == m)) && options.All(t => t.Target != null), "any character target required");
        PlayEntity(g, p, spell, friendly);
        Core2026Proof.Check(friendly.Damage == 3 && !friendly.IsFrozen && enemy.IsFrozen, "three damage, only available enemy frozen");
        ClearBoard(g, p.Opponent); int damage = p.Opponent.Hero.Damage;
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 3 && !p.Opponent.Hero.IsFrozen, "empty board damage works, hero never frozen");
        var enemies = Enumerable.Range(0, 3).Select(_ => Summon(p.Opponent, "CS2_182")).ToArray();
        var clone = g.CloneExact(history: true);
        Play(g, p, id, p.Opponent.Hero); Play(clone, clone.Player1, id, clone.Player2.Hero);
        Core2026Proof.Check(enemies.Count(m => m.IsFrozen) == 2 && HsSim.Canonical.State(g) == HsSim.Canonical.State(clone), "two distinct freezes; seeded clone deterministic");
        foreach (var m in enemies) m.IsFrozen = false;
        enemies[0].Damage = 2; Play(g, p, id, enemies[0]);
        Core2026Proof.Check(enemies[0].Zone == p.Opponent.GraveyardZone && enemies.Skip(1).All(m => m.IsFrozen), "lethally damaged primary excluded from random freeze");
        Summon(p, "TIME_856"); damage = p.Opponent.Hero.Damage;
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 5, "spell power applies to primary damage");
    });
}
