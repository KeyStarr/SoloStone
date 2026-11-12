using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using static TimeTravelS2Proof;

internal static class AeonRendProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_441", (g, p, id) => {
        Play(g, p, id); RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 4, "singleton hero hit once");
        p.Opponent.Hero.Damage = 0;
        var enemy = Summon(p.Opponent, "CS2_186"); enemy.HasDivineShield = true;
        var friendly = Summon(p, "CS2_182");
        Play(g, p, id); RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(!enemy.HasDivineShield && enemy.Damage == 0 && p.Opponent.Hero.Damage == 4 && friendly.Damage == 0,
            "two distinct enemies; Shield blocks only its hit; no friendly damage");
        ParkBoard(p.Opponent); p.Opponent.Hero.Damage = 0;
        Summon(p, "TIME_856");
        var enemies = Enumerable.Range(0, 3).Select(_ => Summon(p.Opponent, "CS2_186")).ToArray();
        Resume(g, next => { Play(next, next.Player1, id); RewindTestHelpers.KeepIfOffered(next); });
        Core2026Proof.Check(enemies.Count(m => m.Damage == 6) + (p.Opponent.Hero.Damage == 6 ? 1 : 0) == 2,
            "spell damage applies once to each of two distinct seeded targets");
    });
}
