using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AxeOfTheForefathersProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_478", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 2 && p.Hero.Weapon.Durability == 2, "printed weapon stats");
        var clone = g.Clone(); Core2026Proof.Check(clone.Process(HeroAttackTask.Any(clone.Player1, clone.Player2.Hero)), "clone hero attack accepted");
        Core2026Proof.Check(friend.Damage == 0 && ((Minion)clone.IdEntityDic[friend.Id]).Damage == 1, "clone attack trigger isolated");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "hero attack accepted");
        Core2026Proof.Check(friend.Damage == 1 && enemy.Damage == 1 && p.Opponent.Hero.Damage == 2, "hero attack damages every minion once, excludes heroes");
        Attack(g, friend, p.Opponent.Hero); Core2026Proof.Check(friend.Damage == 1 && enemy.Damage == 1, "friendly minion attack does not trigger");
        End(g); Attack(g, enemy, p.Hero); End(g);
        Core2026Proof.Check(friend.Damage == 1 && enemy.Damage == 1, "opponent attack does not trigger");
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "hero attack accepted");
        Core2026Proof.Check(p.Hero.Weapon == null && friend.Damage == 2 && enemy.Damage == 2, "last durability attack still triggers");
        Play(g, p, id); Play(g, p, "CS2_106"); End(g); End(g);
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "hero attack accepted");
        Core2026Proof.Check(friend.Damage == 2 && enemy.Damage == 2, "replacement removes old weapon trigger");
    });
}
