using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class InsectClawProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_833", (g, p, id) => {
        var weapon = Play(g, p, id); var clone = g.CloneExact(history: true);
        var task = clone.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == clone.Player2.Hero); Core2026Proof.Check(clone.Process(task), "clone hero attack accepted");
        Core2026Proof.Check(clone.Player1.BoardZone.Count == 1 && p.BoardZone.Count == 0, "clone weapon trigger isolated");
        task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero); Core2026Proof.Check(g.Process(task), "hero attack accepted");
        var grub = p.BoardZone.Single(); Core2026Proof.Check(grub.Card.Id == "TLC_903t" && grub.AttackDamage == 2 && grub.Health == 1 && grub.IsRush, "hero attack summons pinned 2/1 Rush Grub");
        var minion = Summon(p, "CS2_171"); Attack(g, minion, p.Opponent.Hero); Core2026Proof.Check(p.BoardZone.Count == 2, "minion attack does not trigger weapon");
        End(g); Play(g, p.Opponent, "CS2_106"); task = p.Opponent.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Hero); Core2026Proof.Check(g.Process(task), "opponent hero attack accepted"); Core2026Proof.Check(p.BoardZone.Count == 2, "opponent hero does not trigger"); End(g);
        task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero); Core2026Proof.Check(g.Process(task), "final durability attack accepted");
        Core2026Proof.Check(p.Hero.Weapon == null && p.BoardZone.Count == 3, "last durability still summons before weapon breaks");
        Play(g, p, "CS2_106"); End(g); End(g); task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero); Core2026Proof.Check(g.Process(task), "replacement weapon attack"); Core2026Proof.Check(p.BoardZone.Count == 3, "destroyed weapon trigger removed");
        Play(g, p, id); while (p.BoardZone.Count < 7) Summon(p, "CS2_182"); End(g); End(g); task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero); Core2026Proof.Check(g.Process(task), "full-board attack"); Core2026Proof.Check(p.BoardZone.Count == 7, "full board blocks Grub");
    });
}
