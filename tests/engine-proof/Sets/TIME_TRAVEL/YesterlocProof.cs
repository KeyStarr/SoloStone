using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class YesterlocProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_428", (g, p, id) => {
        var m = (Minion)Play(g, p, id); var friendly = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_182"); var held = (Minion)Add(p, "CS2_182");
        Core2026Proof.Check(m.IsRace(Race.MURLOC), "Murloc identity");
        friendly.Damage = 2;
        var clone = g.CloneExact(history: true); End(clone);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[friendly.Id]).Health == 4 && friendly.Health == 3, "clone end buff isolated");
        End(g);
        Core2026Proof.Check(friendly.BaseHealth == 6 && friendly.Damage == 2 && friendly.Health == 4 && friendly.AppliedEnchantments.Single().Card.Id == "TIME_428e", "other friendly minion gains permanent health, damage preserved");
        Core2026Proof.Check(m.BaseHealth == 1 && enemy.BaseHealth == 5 && held.BaseHealth == 5, "excludes self, enemy and hand");
        End(g); Core2026Proof.Check(friendly.BaseHealth == 6, "opponent end does not trigger");
        Play(g, p, "BE_228", m); End(g); End(g);
        Core2026Proof.Check(friendly.BaseHealth == 6, "Silence removes own end trigger; granted health persists");
        friendly.Silence(); Core2026Proof.Check(friendly.BaseHealth == 5 && friendly.Damage == 1 && friendly.Health == 4, "Silence removes local bonus Health and corresponding damage");
        var gone = Summon(p, id); Kill(g, gone); End(g);
        Core2026Proof.Check(friendly.BaseHealth == 5, "dead source does not trigger");
    });
}
