using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static EmeraldDreamBatch3Proof;

internal static class UrsineMaulProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_253", (g, p, id) => {
        var weapon = (Weapon)Play(g, p, id);
        Core2026Proof.Check(weapon.AttackDamage == 4 && weapon.Durability == 2, "printed 4/2 weapon");
        var clone = g.Clone();
        var task = clone.Player1.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == clone.Player2.Hero);
        Core2026Proof.Check(clone.Process(task) && clone.Player1.HandZone.Count == 1 && p.HandZone.Count == 0, "clone attack draw isolated");
        task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero);
        Core2026Proof.Check(g.Process(task) && p.HandZone.Count == 1 && p.Opponent.Hero.Damage == 4, "hero attack draws once");
        var minion = Summon(p, "CS2_171"); Attack(g, minion, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == 1, "minion attack does not draw");
        End(g); Play(g, p.Opponent, "CS2_106");
        int hand = p.HandZone.Count;
        task = p.Opponent.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Hero);
        Core2026Proof.Check(g.Process(task) && p.HandZone.Count == hand, "opponent hero attack does not draw");
        End(g); hand = p.HandZone.Count;
        task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero);
        Core2026Proof.Check(g.Process(task) && p.HandZone.Count == hand + 1 && p.Hero.Weapon == null, "last durability draws before break");
        Play(g, p, "CS2_106"); End(g); End(g); hand = p.HandZone.Count;
        task = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.Target == p.Opponent.Hero);
        Core2026Proof.Check(g.Process(task) && p.HandZone.Count == hand, "destroyed weapon no longer triggers");
    });
}
