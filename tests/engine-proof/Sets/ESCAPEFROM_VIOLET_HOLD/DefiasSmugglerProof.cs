using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static SilenceKeywordsProof;

internal static class DefiasSmugglerProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_998", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_120");
        var noTarget = (Minion)Play(g, p, id);
        Core2026Proof.Check(noTarget.AttackDamage == 2 && noTarget.Health == 3 && !noTarget.IsRush, "no friendly minion: no target, no self buff");
        var fresh = Summon(p, "CS2_182");
        var held = Add(p, id); p.UsedMana = 0;
        var options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == held.Id).ToArray();
        Core2026Proof.Check(options.Length > 0 && options.All(t => t.Target is Minion m && m.Controller == p)
            && options.Select(t => t.Target.Id).Distinct().OrderBy(x => x).SequenceEqual(new[] { noTarget.Id, fresh.Id }.OrderBy(x => x)),
            "friendly minion required when available; enemy minions, heroes and no-target excluded");
        PlayEntity(g, p, held, fresh);
        Core2026Proof.Check(fresh.AttackDamage == 6 && fresh.Health == 5 && fresh.IsRush
            && fresh.AppliedEnchantments.Count(e => e.Card.Id == "JAIL_998e") == 1 && enemy.AttackDamage == 2, "pinned JAIL_998e +2 Attack and Rush");
        Core2026Proof.Check(CanAttack(fresh, enemy) && !CanAttack(fresh, p.Opponent.Hero), "fresh target attacks minions only");
        AttackOptionsProof.Lifecycle(g, fresh, enemy, rush: true);
        var prepared = HijackedSecuritybotProof.Prepared(g, p, id);
        var older = noTarget;
        PlayEntity(g, p, prepared, older);
        Core2026Proof.Check(older.AttackDamage == 4 && older.Health == 3 && older.IsRush && CanAttack(older, p.Opponent.Hero),
            "prepared play buffs an older minion, which keeps hero attacks");
        Play(g, p, "BE_228", older);
        Core2026Proof.Check(older.AttackDamage == 2 && !older.IsRush && CanAttack(older, p.Opponent.Hero), "Silence removes buff, older minion stays ready");
    });
}
