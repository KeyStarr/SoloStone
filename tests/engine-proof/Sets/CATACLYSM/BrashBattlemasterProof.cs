using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BrashBattlemasterProof
{
    internal static void Run() => StandardSetsProof.Case("MEND_800", (g, p, id) => {
        var silenced = (Minion)Play(g, p, id); Core2026Proof.Check(silenced.IsRush, "printed Rush"); silenced.Silence(); Kill(g, silenced);
        var control = Summon(p, "CS2_101t"); Core2026Proof.Check(control.AttackDamage == 1, "Silence suppresses Deathrattle");
        VerifyRecruitBuff(g, p, id, 1, 0, (game, owner) => Kill(game, (Minion)Play(game, owner, id)));
    });

    internal static void VerifyRecruitBuff(Game g, Controller p, string id, int attack, int health, Action<Game, Controller> activate)
    {
        var board = Summon(p, "CS2_101t"); var hand = (Minion)Add(p, "CS2_101t");
        var other = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_101t");
        var clone = g.CloneExact(history: true); activate(clone, clone.Player1);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[board.Id]).AttackDamage == 1 + attack && ((Minion)clone.IdEntityDic[board.Id]).Health == 1 + health && board.AttackDamage == 1 && board.Health == 1, $"clone persistent aura isolated: clone={((Minion)clone.IdEntityDic[board.Id]).AttackDamage}/{((Minion)clone.IdEntityDic[board.Id]).Health} original={board.AttackDamage}/{board.Health} buffs={string.Join(",", p.AppliedEnchantments?.Select(e => e.Card.Id) ?? Enumerable.Empty<string>())}");
        activate(g, p);
        Core2026Proof.Check(board.AttackDamage == 1 + attack && board.Health == 1 + health && hand.AttackDamage == 1 + attack && hand.Health == 1 + health, "existing board and hand Recruits buffed");
        Core2026Proof.Check(other.AttackDamage == 4 && other.Health == 5 && enemy.AttackDamage == 1 && enemy.Health == 1, "non-Recruits and opponent excluded");
        End(g); End(g);
        Core2026Proof.Check(board.AttackDamage == 1 + attack && board.Health == 1 + health, "game buff survives turn boundary and source departure");
        var fresh = Summon(p, "CS2_101t"); Core2026Proof.Check(fresh.AttackDamage == 1 + attack && fresh.Health == 1 + health, "future summon buffed");
        board.Silence(); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(board.AttackDamage == 1 + attack && board.Health == 1 + health, "controller aura survives Recruit Silence");
        activate(g, p); Core2026Proof.Check(fresh.AttackDamage == 1 + 2 * attack && fresh.Health == 1 + 2 * health, "independent game buffs stack");
    }
}
