using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class HauntProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_801", (g, p, id) => {
        var target = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, target, false);
        var spell = p.HandZone.Single(c => c.Card.Id == id); PlayEntity(g, p, spell, target);
        Core2026Proof.Check(target.AttackDamage == 6 && target.BaseHealth == 8 && target.HasTaunt && target[GameTag.REBORN] == 1, "stats Taunt and Reborn granted");
        var clone = g.Clone(); Kill(clone, (Minion)clone.IdEntityDic[target.Id]);
        var reborn = clone.Player1.BoardZone.Single();
        Core2026Proof.Check(reborn.Card.Id == "CS2_182" && reborn.AttackDamage == 4 && reborn.Health == 1 && !reborn.HasTaunt && reborn[GameTag.REBORN] == 0 && target.Health == 8, "ordinary Reborn loses buffs, consumes keyword, clone isolated");
        Play(clone, clone.Player1, id, reborn); Kill(clone, reborn);
        Core2026Proof.Check(clone.Player1.BoardZone.Single().Health == 1, "regrant resets consumed Reborn");
        target.Silence(); Core2026Proof.Check(target.AttackDamage == 4 && target.Health == 5 && !target.HasTaunt && target[GameTag.REBORN] == 0, "Silence removes full enchantment");
        Kill(g, target); Core2026Proof.Check(p.BoardZone.Count == 0, "silenced target stays dead");
        Play(g, p, id, enemy); Kill(g, enemy); Core2026Proof.Check(p.Opponent.BoardZone.Single().Health == 1, "enemy minion may receive Reborn"); g.DeathProcessingAndAuraUpdate();
    });
}
