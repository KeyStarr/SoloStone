using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class BrassknuckleBrawlerProof
{
    internal static void Run() => StandardSetsProof.Case("BE_560", (g, p, id) => {
        p.Hero.Armor = 9; var below = (Minion)Play(g, p, id);
        Core2026Proof.Check(below.AttackDamage == 6 && below.Health == 6 && below.HasCharge, "9 Armor does not buff; Charge intrinsic");
        var enemy = Summon(p.Opponent, "CS2_182");
        AttackOptionsProof.Lifecycle(g, below, enemy, rush: false);
        p.Hero.Armor = 10; var boundary = (Minion)Play(g, p, id);
        Core2026Proof.Check(boundary.AttackDamage == 10 && boundary.Health == 10 && boundary.HasCharge && boundary.AppliedEnchantments.Any(e => e.Card.Id == "BE_560e"), "10 Armor grants pinned +4/+4");
        AttackOptionsProof.Lifecycle(g, boundary, enemy, rush: false);
        p.Hero.Armor = 0; Core2026Proof.Check(boundary.AttackDamage == 10, "buff persists after Armor loss");
        Play(g, p, "BE_228", boundary); Core2026Proof.Check(boundary.AttackDamage == 6 && boundary.Health == 6 && !boundary.HasCharge, "silence removes buff and Charge");
    });
}
