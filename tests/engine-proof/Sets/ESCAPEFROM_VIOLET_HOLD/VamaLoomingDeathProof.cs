using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class VamaLoomingDeathProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_118", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var paladin = Summon(p, "EX1_362"); var enemyPaladin = Summon(p.Opponent, "EX1_362");
        var multiclass = Summon(p, "CFM_321");
        var portal = Summon(p, "UNG_829t2"); var enemyPortal = Summon(p.Opponent, "UNG_829t2");
        Core2026Proof.Check(portal.Untouchable && enemyPortal.Untouchable && !portal.IsDormant && !enemyPortal.IsDormant, "legacy untouchable fixtures are not dormant");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(friendly.Zone.Type == Zone.GRAVEYARD && enemy.Zone.Type == Zone.GRAVEYARD, "destroys non-Paladins on both boards");
        Core2026Proof.Check(source.Zone.Type == Zone.PLAY && paladin.Zone.Type == Zone.PLAY && enemyPaladin.Zone.Type == Zone.PLAY, "Paladins and source survive regardless of controller class");
        Core2026Proof.Check(multiclass.Zone.Type == Zone.PLAY, "multi-class Paladin survives");
        Core2026Proof.Check(portal.Zone.Type == Zone.PLAY && enemyPortal.Zone.Type == Zone.PLAY, "untouchable non-Paladins survive on both boards");
        ClearBoard(g, p); ClearBoard(g, p.Opponent); source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Single() == source && p.Opponent.BoardZone.Count == 0, "empty board Battlecry is safe");
    });
}
