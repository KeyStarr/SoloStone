using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class UndercoverCultistCorrectionProof
{
    // Tier D: pinned 37.0.0.254477 TLC_101 (2/3, +3 while damaged), TLC_101e (+3 Attack).
    // Owner-approved existing-card correction, 2026-10-22; replay audit: docs/reports/b14-k2-corrections.md.
    [GroupProof("b14-k2-cultist-correction")]
    internal static void Run()
    {
        for (int seed = 1; seed <= 3; seed++)
        foreach (bool reborn in new[] { false, true })
        {
            var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1,
                SkipMulligan = true, History = true, Logging = false });
            game.StartGame();
            var cultist = (Minion)Play(game, game.Player1, "TLC_101");
            if (reborn)
            {
                Kill(game, cultist);
                cultist = game.Player1.BoardZone.Single(m => m.Card.Id == "TLC_101");
            }
            else
            {
                cultist.TakeDamage(game.Player2.Hero, 1);
                game.DeathProcessingAndAuraUpdate();
            }
            Core2026Proof.Check(cultist.AttackDamage == 5, "damaged original/reborn Cultist has five Attack before Silence");
            // Complete the summon/turn checkpoint before testing the saved decision boundary.
            End(game); End(game);
            foreach (var copy in new[] { game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()), game })
            {
                var body = (Minion)copy.IdEntityDic[cultist.Id];
                body.Silence(); copy.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(body.AttackDamage == 2, "Cultist Silence restores printed two Attack without subtracting bonus twice");
                Core2026Proof.Check(!body.HasTaunt && body[GameTag.REBORN] == 0, "Cultist Silence removes printed keywords");
                Kill(copy, body);
                Core2026Proof.Check(!copy.Player1.BoardZone.Any(m => m.Card.Id == "TLC_101"), "silenced Cultist stays dead");
            }
        }
        Console.WriteLine("PASS b14-k2 Cultist correction: damaged and reborn Silence, seeds 1-3, clone/load");
    }
}
