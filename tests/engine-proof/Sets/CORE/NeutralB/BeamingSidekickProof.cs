using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BeamingSidekickProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_ULD_191", (g, p, id) => {
        var first = (Minion)Play(g, p, id);
        Core2026Proof.Check(first.AttackDamage == 1 && first.Health == 2, "empty board play has no self buff");
        var enemy = Summon(p.Opponent, "CS2_182");
        var source = Add(p, id); p.UsedMana = 0;
        var options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(options.Length > 0 && options.All(t => t.Target?.Id == first.Id), "only friendly existing minion can be targeted; heroes and enemies excluded");
        PlayEntity(g, p, source, first);
        Core2026Proof.Check(first.AttackDamage == 1 && first.BaseHealth == 4 && enemy.Health == 5,
            "battlecry grants exactly +2 Health to friendly target");
        Core2026Proof.Check(first.AppliedEnchantments.Any(e => e.Card.Id == "ULD_191e"), "exact pinned Assisting enchantment");
        first.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(first.Health == 2, "Silence removes granted Health");
        var summoned = Summon(p, id); Core2026Proof.Check(summoned.Health == 2 && first.Health == 2, "summon skips battlecry");
        var recruit = Summon(p, "CS2_101t"); var champion = Summon(p, "CS2_222");
        var buffer = (Minion)Play(g, p, id, recruit);
        recruit.TakeDamage(p.Opponent.Hero, 3);
        Core2026Proof.Check(recruit.BaseHealth == 4 && recruit.Damage == 3, "granted Health stacks with external aura");
        Kill(g, buffer);
        Core2026Proof.Check(recruit.BaseHealth == 4 && recruit.Damage == 3, "battlecry buff persists after granting source dies");
        recruit.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(recruit.BaseHealth == 2 && recruit.Damage == 1 && recruit.Health == 1,
            "Silence removes local Health buff while preserving damage under external aura");
        ClearBoard(g, p); Play(g, p, id);
        Core2026Proof.Check(enemy.Health == 5, "enemy-only board permits untargeted play");
    });
}
