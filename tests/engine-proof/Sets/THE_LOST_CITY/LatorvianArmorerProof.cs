using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LatorvianArmorerProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_606", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.Hero.Armor == 0, "optional enemy target on empty board");
        var lethal = Summon(p.Opponent, "EX1_506a"); Play(g, p, id, lethal);
        Core2026Proof.Check(lethal.Zone.Type == Zone.GRAVEYARD && p.Hero.Armor == 5, "kill gains five Armor");
        var survivor = Summon(p.Opponent, "CS2_182"); Play(g, p, id, survivor);
        Core2026Proof.Check(survivor.Health == 3 && p.Hero.Armor == 5, "survivor grants no Armor");
        var shield = Summon(p.Opponent, "EX1_008"); shield.HasDivineShield = true; Play(g, p, id, shield);
        Core2026Proof.Check(shield.Health == 1 && p.Hero.Armor == 5, "shield blocks kill and Armor");
        TargetRules(g, p, id, survivor, p.BoardZone[0], true);
    });
}
