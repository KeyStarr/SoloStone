using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ReleasetheBeastsProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_387", (g, p, id) => {

        var ordinary = (Minion)Add(p, "CS2_182"); var legendary = (Minion)Add(p, "EX1_016");
        var spell = Add(p, "CS2_029"); var enemy = (Minion)Add(p.Opponent, "CS2_182"); var board = Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(ordinary.AttackDamage == 5 && ordinary.Health == 6 && legendary.AttackDamage == 8 && legendary.Health == 7, "ordinary +1/+1; Legendary total +3/+2");
        Core2026Proof.Check(enemy.AttackDamage == 4 && enemy.Health == 5 && board.AttackDamage == 4 && board.Health == 5 && spell.Cost == 4, "only own held minions buffed");
        Play(g, p, id); Core2026Proof.Check(ordinary.AttackDamage == 6 && ordinary.Health == 7 && legendary.AttackDamage == 11 && legendary.Health == 9, "buffs stack");
        var clone = g.Clone(); PlayEntity(clone, clone.Player1, clone.IdEntityDic[ordinary.Id]);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[ordinary.Id]).Health == 7 && ordinary.Zone == p.HandZone, "hand buffs survive play in isolated clone");
        PlayEntity(g, p, ordinary); ordinary.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(ordinary.AttackDamage == 4 && ordinary.Health == 5, "silence removes both hand buffs after play");
        foreach (var held in p.HandZone.ToArray()) { p.HandZone.Remove(held); p.SetasideZone.Add(held); }
        Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == 0, "empty hand legal");
    });
}
