using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CrystalTuskProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_408", (g, p, id) => {
        var first = Add(p, "CS2_182"); var second = Add(p, "CS2_171");
        int deck = p.DeckZone.Count;
        Play(g, p, id);
        Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 2 && p.Hero.Weapon.Durability == 2, "2/2 weapon");
        Core2026Proof.Check(first.Zone == p.DeckZone && second.Zone == p.HandZone && p.DeckZone.Count == deck + 1, "only leftmost hand card shuffled");
        p.Hero.Weapon.Destroy(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.DeckZone.Count == deck - 1 && p.HandZone.Count == 3, "destroyed weapon draws two");
        foreach (var held in p.HandZone.ToArray()) { p.HandZone.Remove(held); p.SetasideZone.Add(held); }
        deck = p.DeckZone.Count; Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 0 && p.DeckZone.Count == deck, "empty hand shuffle is no-op");
        while (p.HandZone.Count < 9) Add(p, "CS2_182");
        p.Hero.Weapon.Destroy(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 2, "deathrattle burns excess draw at hand limit");
    });
}
