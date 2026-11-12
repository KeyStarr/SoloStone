using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SynchronizedSparkProof
{
    internal static void Run() => StandardSetsProof.Case("END_014", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var spell = Add(p, id);
        var targets = p.Options().Where(t => t.Source?.Id == spell.Id).ToArray();
        Core2026Proof.Check(targets.Any(t => t.Target == enemy) && targets.Any(t => t.Target == p.Opponent.Hero) && targets.All(t => t.Target?.Controller == p.Opponent), "enemy character required");
        PlayEntity(g, p, spell, enemy);
        Core2026Proof.Check(enemy.Health == 2 && friendly.AttackDamage == 4, "surviving target gives no buff");
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && friendly.AttackDamage == 7 && friendly.Health == 8 && friendly.AppliedEnchantments.Single().Card.Id == "END_014e", "lethal grants exact +3/+3");
        friendly.Silence(); Core2026Proof.Check(friendly.AttackDamage == 4 && friendly.Health == 5, "buff removable by Silence");
        enemy = Summon(p.Opponent, "TIME_056"); Play(g, p, id, enemy);
        Core2026Proof.Check(!enemy.HasDivineShield && enemy.Health == 1 && friendly.AttackDamage == 4, "Shield prevents kill and buff");
        ClearBoard(g, p); Play(g, p, id, enemy); Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && p.BoardZone.Count == 0, "lethal with no friendly minions safe");
        friendly = Summon(p, "CS2_182"); Summon(p, "TIME_856"); enemy = Summon(p.Opponent, "CS2_182");
        var clone = g.CloneExact(history: true); Play(g, p, id, enemy); Play(clone, clone.Player1, id, (Minion)clone.IdEntityDic[enemy.Id]);
        Core2026Proof.Check(p.BoardZone.Sum(m => m.AttackDamage) == 11 && HsSim.Canonical.State(g) == HsSim.Canonical.State(clone), "spell power kills five health; seeded buff deterministic");
    });
}
