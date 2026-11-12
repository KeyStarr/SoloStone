using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ReadyTheFleetProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_441", (g, p, id) => {
        var target = Summon(p, "CORE_LOE_039");
        var beast = Summon(p, "CS2_171"); var mech = Summon(p, "EX1_556");
        var none = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_171");
        var held = (Minion)Add(p, "CS2_171");
        var spell = Add(p, id); p.UsedMana = 0;
        var options = p.Options().Where(t => t.Source?.Id == spell.Id).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target?.Id == target.Id) && options.All(t => t.Target is Minion m && m.Controller == p), "friendly minion targets only");
        PlayEntity(g, p, spell, target);
        Core2026Proof.Check(target.AttackDamage == 4 && target.Health == 6 && beast.AttackDamage == 2 && beast.Health == 3 && mech.AttackDamage == 3 && mech.Health == 5, "both components share type; dual target buffed once");
        Core2026Proof.Check(none.AttackDamage == 4 && none.Health == 5 && enemy.Health == 1 && held.Health == 1, "typeless, opponent and hand excluded");
        Play(g, p, id, none);
        Core2026Proof.Check(none.AttackDamage == 5 && none.Health == 7 && target.AttackDamage == 4, "typeless target gets buff alone");
        target.Silence(); Core2026Proof.Check(target.AttackDamage == 3 && target.Health == 4, "Silence removes fleet buff");
        Core2026Proof.Check(beast.AppliedEnchantments.Any(e => e.Card.Id == "TLC_441e2"), "exact +1/+2 enchantment");
    });
}
