using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ArcaneBarrageProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_855", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var primary = Summon(p.Opponent, "CS2_182");
        var other = Summon(p.Opponent, "CS2_182"); var spell = Add(p, id);
        var options = p.Options().Where(t => t.Source?.Id == spell.Id).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target == primary) && options.Any(t => t.Target == p.Opponent.Hero) && options.All(t => t.Target?.Controller == p.Opponent), "enemy character target only");
        PlayEntity(g, p, spell, primary);
        Core2026Proof.Check(primary.Damage == 3 && other.Damage == 2 && p.Opponent.Hero.Damage == 2 && friendly.Damage == 0, "three to primary and two to each of two other enemies");
        ClearBoard(g, p.Opponent); int damage = p.Opponent.Hero.Damage;
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 3, "no secondary target never repeats primary");
        primary = Summon(p.Opponent, "TIME_056"); damage = p.Opponent.Hero.Damage;
        Play(g, p, id, primary);
        Core2026Proof.Check(!primary.HasDivineShield && primary.Damage == 0 && p.Opponent.Hero.Damage == damage + 2, "shielded primary excluded; single secondary hit once");
        ClearBoard(g, p.Opponent); Summon(p, "TIME_856");
        var enemies = Enumerable.Range(0, 3).Select(_ => Summon(p.Opponent, "CS2_186")).ToArray();
        var clone = g.CloneExact(history: true); damage = p.Opponent.Hero.Damage;
        Play(g, p, id, enemies[0]); Play(clone, clone.Player1, id, (Minion)clone.IdEntityDic[enemies[0].Id]);
        Core2026Proof.Check(enemies[0].Damage == 5 && enemies.Skip(1).Count(m => m.Damage == 4) + (p.Opponent.Hero.Damage == damage + 4 ? 1 : 0) == 2, "spell power applies to distinct primary and secondary hits");
        Core2026Proof.Check(HsSim.Canonical.State(g) == HsSim.Canonical.State(clone), "seeded secondary targets deterministic after clone");
    });
}
