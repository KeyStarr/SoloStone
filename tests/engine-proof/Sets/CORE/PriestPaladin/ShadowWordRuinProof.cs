using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ShadowWordRuinProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_197", (g, p, id) => {
        var low = Summon(p, "CS2_182");
        var equal = Summon(p, "CS2_182"); equal.AttackDamage = 5;
        var high = Summon(p.Opponent, "CS2_182"); high.AttackDamage = 6; high.HasDivineShield = true;
        var reduced = Summon(p.Opponent, "CS2_200"); reduced.AttackDamage = 4;
        var dormant = Summon(p.Opponent, "CS2_182"); dormant.AttackDamage = 8; dormant[GameTag.DORMANT] = 1;
        Play(g, p, id);
        Core2026Proof.Check(equal.Zone.Type == Zone.GRAVEYARD && high.Zone.Type == Zone.GRAVEYARD, "five-plus current Attack destroyed on both sides through shield");
        Core2026Proof.Check(low.Zone == p.BoardZone && reduced.Zone == p.Opponent.BoardZone && dormant.Zone == p.Opponent.BoardZone, "four Attack and Dormant survive");
        Core2026Proof.Check(p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "heroes untouched");
        dormant[GameTag.DORMANT] = 0; ClearBoard(g, p); ClearBoard(g, p.Opponent); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 0 && p.Opponent.BoardZone.Count == 0, "empty board legal");
    });
}
