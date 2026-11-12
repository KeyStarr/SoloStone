using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class BigGameHunterProof
{
    internal static void Run() => Case("CORE_EX1_005", (g, p, id) => {
        var friendly = Summon(p, "CS2_186");
        var enemy = Summon(p.Opponent, "CS2_186");
        var small = Summon(p.Opponent, "CS2_200");
        var source = Add(p, id);
        var plays = p.Options().Where(t => t is PlayCardTask && t.Source == source).ToArray();
        Core2026Proof.Check(plays.Any(t => t.Target == friendly) && plays.Any(t => t.Target == enemy)
            && plays.All(t => t.Target == friendly || t.Target == enemy), "Hunter targets either side at 7 Attack, excludes 6");
        PlayEntity(g, p, source, enemy);
        Play(g, p, id, friendly);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && friendly.Zone.Type == Zone.GRAVEYARD
            && small.Zone == p.Opponent.BoardZone, "Hunter destroys chosen 7 Attack minions on either side");
        Play(g, p, id);
        CoreTradeProof.Verify(g, p, id);
    });
}
