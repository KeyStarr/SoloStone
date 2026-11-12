using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DeepFreezeProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BT_072", (g, p, id) => {
        var left = Summon(p, "CS2_182");
        var source = Add(p, id);
        var options = p.Options().Where(t => t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target == p.Opponent.Hero) && options.All(t => t.Target?.Controller == p.Opponent), "only enemy characters are targets");
        PlayEntity(g, p, source, p.Opponent.Hero);
        var waters = p.BoardZone.Where(m => m.Card.Id == "CS2_033").OrderBy(m => m.Id).ToArray();
        Core2026Proof.Check(p.Opponent.Hero.IsFrozen && waters.Length == 2 && waters.All(m => m.AttackDamage == 3 && m.Health == 6 && m.IsRace(Race.ELEMENTAL)), "freeze enemy hero and exact Water Elementals");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id }.Concat(waters.Select(m => m.Id))), "spell summons append in creation order");
        var enemy = Summon(p.Opponent, "CS2_182"); Attack(g, waters[0], enemy);
        Core2026Proof.Check(enemy.IsFrozen && enemy.Damage == 3, "Water Elemental retains damage Freeze");
        foreach (int count in new[] { 6, 7 })
        {
            ClearBoard(g, p); for (int i = 0; i < count; i++) Summon(p, "CS2_182");
            enemy.IsFrozen = false; Play(g, p, id, enemy);
            Core2026Proof.Check(enemy.IsFrozen && p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == "CS2_033") == 7 - count, "Freeze resolves even with partial/full board");
        }
    });
}
