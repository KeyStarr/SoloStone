using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CommandClawProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_467", (g, p, id) => {
        Play(g, p, id); var friend = Summon(p, "CS2_182"); var clone = g.Clone();
        var task = clone.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == clone.Player2.Hero);
        Core2026Proof.Check(clone.Process(task) && ((Minion)clone.IdEntityDic[friend.Id]).AttackDamage == 6 && friend.AttackDamage == 4, "clone weapon trigger isolated");
        task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero);
        Core2026Proof.Check(g.Process(task) && friend.AttackDamage == 6 && friend.Health == 5, "hero attack grants sole friendly minion two Attack");
        Attack(g, friend, p.Opponent.Hero); Core2026Proof.Check(friend.AttackDamage == 6, "minion attack does not trigger");
        End(g); Play(g, p.Opponent, "CS2_106"); task = p.Opponent.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Hero);
        Core2026Proof.Check(g.Process(task) && friend.AttackDamage == 6, "opponent hero does not trigger"); End(g);
        task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero);
        Core2026Proof.Check(g.Process(task) && friend.AttackDamage == 8 && p.Hero.Weapon == null, "last durability attack buffs before weapon breaks");
        friend.Silence(); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(friend.AttackDamage == 4, "Silence removes weapon-granted buffs");
        Play(g, p, "CS2_106"); End(g); End(g); task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero);
        Core2026Proof.Check(g.Process(task) && friend.AttackDamage == 4, "destroyed weapon no longer triggers");
        ClearBoard(g, p); Play(g, p, id); End(g); End(g); task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero);
        Core2026Proof.Check(g.Process(task) && p.BoardZone.Count == 0, "empty friendly board safely skips random buff");
    });
}
