using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TwistedTreantProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_495", (g, p, id) => {
        var friendly = (Minion)Add(p, "CS2_182"); var enemy = (Minion)Add(p.Opponent, "EX1_008");
        var spell = Add(p, "CS2_029"); var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(friendly.AttackDamage == 4 && enemy.AttackDamage == 1, "debuff waits for death"); Kill(g, source);
        Core2026Proof.Check(friendly.AttackDamage == 2 && enemy.AttackDamage == 0 && friendly.Health == 5 && enemy.Health == 1, "each hand minion loses two Attack with zero floor");
        Core2026Proof.Check(friendly.AppliedEnchantments.Any(e => e.Card.Id == "EDR_495e") && enemy.AppliedEnchantments.Any(e => e.Card.Id == "EDR_495e") && spell.AppliedEnchantments?.Count is null or 0, "exact debuff on both players and no spell buff");
        PlayEntity(g, p, friendly); Core2026Proof.Check(friendly.AttackDamage == 2, "hand debuff persists on play"); friendly.Silence(); Core2026Proof.Check(friendly.AttackDamage == 4, "silence removes debuff");
        var candidates = new[] { (Minion)Add(p, "CS2_182"), (Minion)Add(p, "CS2_182") };
        source = Summon(p, id); Kill(g, source); Core2026Proof.Check(candidates.Count(m => m.AttackDamage == 2) == 1, "exactly one random hand minion debuffed");
        foreach (var owner in new[] { p, p.Opponent }) foreach (var card in owner.HandZone.ToArray()) { owner.HandZone.Remove(card); owner.SetasideZone.Add(card); }
        spell = Add(p, "CS2_029"); source = Summon(p, id); Kill(g, source); Core2026Proof.Check(spell.AppliedEnchantments?.Count is null or 0, "empty or spell-only hands are safe");
        friendly = (Minion)Add(p, "CS2_182"); source = Summon(p, id); source.Silence(); Kill(g, source); Core2026Proof.Check(friendly.AttackDamage == 4, "silence removes Deathrattle");
    });
}
