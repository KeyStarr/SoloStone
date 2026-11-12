using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TheUnknowableOneProof
{
    internal static void Run() => StandardSetsProof.Case("BE_725", (g, p, id) => {
        var combo = (Minion)Add(p, "EX1_134"); var plain = (Minion)Add(p, "CS2_182"); var spell = Add(p, "EX1_124");
        combo.AttackDamage = 9; combo.BaseHealth = 10;
        Play(g, p, id);
        Core2026Proof.Check(combo.AttackDamage == 6 && combo.Health == 6 && combo.AppliedEnchantments.Any(e => e.Card.Id == "BE_725e2"), "sets buffed Combo minion to 6/6");
        Core2026Proof.Check(plain.AttackDamage == 4 && plain.Health == 5 && (spell.AppliedEnchantments?.Count ?? 0) == 0, "noncombo minion and Combo spell unchanged");
        Play(g, p, id); Core2026Proof.Check(combo.AttackDamage == 6 && combo.Health == 6, "repeated set stays 6/6");
        PlayEntity(g, p, combo, p.Opponent.Hero); Play(g, p, "BE_228", combo); Core2026Proof.Check(combo.AttackDamage == 3 && combo.Health == 3, "silence clears stat set");
    });
}
