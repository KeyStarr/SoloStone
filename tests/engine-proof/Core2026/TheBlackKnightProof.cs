using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class TheBlackKnightProof
{
    internal static void Run() => Case("CORE_EX1_002", (g, p, id) => {
        var friendly = Summon(p, "CS2_179");
        var enemy = Summon(p.Opponent, "CS2_179");
        var plain = Summon(p.Opponent, "CS2_182");
        var source = Add(p, id);
        var plays = p.Options().Where(t => t is PlayCardTask && t.Source == source).ToArray();
        Core2026Proof.Check(plays.Length > 0 && plays.All(t => t.Target == enemy), "Black Knight only targets enemy Taunt");
        PlayEntity(g, p, source, enemy);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && friendly.Zone == p.BoardZone && plain.Zone == p.Opponent.BoardZone,
            "Black Knight destroys only chosen enemy Taunt");
        Play(g, p, id);
        CoreTradeProof.Verify(g, p, id);
    });
}
