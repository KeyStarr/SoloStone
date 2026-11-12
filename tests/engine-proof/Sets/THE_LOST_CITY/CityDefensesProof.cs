using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CityDefensesProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_622", (g, p, id) => {
        Play(g, p, id); var guards = p.BoardZone.ToArray();
        Core2026Proof.Check(guards.Length == 2 && guards.All(m => m.Card.Id == "TLC_622t" && m.AttackDamage == 0 && m.Health == 6 && m.HasTaunt && m.Card.Implemented), "two pinned 0/6 Taunt Security");
        Core2026Proof.Check(guards[0].Id < guards[1].Id, "Security summoned in creation order");
        var clone = g.CloneExact(history: true); Play(clone, clone.Player1, "CS2_008", (Minion)clone.IdEntityDic[guards[0].Id]);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[guards[0].Id]).AttackDamage == 1 && guards[0].AttackDamage == 0, "clone damage buff isolated");
        Play(g, p, "CS2_008", guards[0]); Core2026Proof.Check(guards[0].AttackDamage == 1 && guards[1].AttackDamage == 0, "only damaged Security gains one Attack");
        End(g); Play(g, p.Opponent, "CS2_008", guards[0]); Core2026Proof.Check(guards[0].AttackDamage == 2, "enemy damage also triggers"); End(g);
        Core2026Proof.Check(guards[0].AttackDamage == 2, "buff persists across turns");
        Play(g, p, "BE_228", guards[0]); Play(g, p, "CS2_008", guards[0]); Core2026Proof.Check(guards[0].AttackDamage == 0, "Silence removes buff and damage trigger");
        Kill(g, guards[1]); Core2026Proof.Check(guards[1].ActivatedTrigger == null, "dead Security trigger removed"); ClearBoard(g, p);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182"); Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "TLC_622t") == 1, "one free slot summons one Security");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7, "full board cannot summon");
    });
}
