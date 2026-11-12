using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Actions;

internal static class SilenceAuraHealthProof
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("SilenceAuraHealth: " + message);
    }

    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        foreach (int sources in new[] { 1, 2 })
        {
            var game = HeroDivineShieldProof.NewGame(history);
            var recruit = HeroDivineShieldProof.Summon(game, "CS2_101t");
            for (int i = 0; i < sources; i++) HeroDivineShieldProof.Summon(game, "CS2_222");
            recruit.TakeDamage(game.Player2.Hero, 1);
            Check(recruit.BaseHealth == 1 + sources && recruit.Damage == 1, "damaged aura recipient fixture");
            foreach (var resumed in new[] { game, game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var target = (Minion)resumed.IdEntityDic[recruit.Id];
                target.Silence();
                resumed.DeathProcessingAndAuraUpdate();
                Check(target.BaseHealth == 1 + sources && target.Damage == 1 && target.Health == sources,
                    "retained external aura never heals damaged recipient on Silence");
                target.Silence();
                Check(target.Damage == 1 && target.Health == sources, "repeated Silence preserves damage");
            }
        }
        foreach (bool history in new[] { false, true })
        {
            var game = HeroDivineShieldProof.NewGame(history);
            var recruit = HeroDivineShieldProof.Summon(game, "CS2_101t");
            var champion = HeroDivineShieldProof.Summon(game, "CS2_222");
            recruit.Silence();
            Check(recruit.BaseHealth == 2 && recruit.Damage == 0, "undamaged recipient retains aura");
            champion.Silence();
            game.DeathProcessingAndAuraUpdate();
            Check(recruit.BaseHealth == 1, "silencing source removes its outgoing aura");

            var plain = HeroDivineShieldProof.Summon(game, "CS2_120");
            champion.Silence();
            game.DeathProcessingAndAuraUpdate();
            plain.TakeDamage(game.Player2.Hero, 1);
            plain.Silence();
            Check(plain.BaseHealth == 3 && plain.Damage == 1 && plain.Health == 2,
                "ordinary damaged minion without aura stays unchanged");
        }
        LocalHealthRemoval();
        Console.WriteLine("PASS Silence aura Health: retained/stacked auras, damage, source Silence, local buff removal without false death, clone/snapshot, history on/off");
    }

    private static void LocalHealthRemoval()
    {
        foreach (bool history in new[] { false, true })
        foreach (int sources in new[] { 0, 1, 2 })
        foreach (int damage in new[] { 0, 3, 4, 5 })
        {
            if (sources == 0 && damage == 5) continue; // The fixture must survive before Silence.
            var game = HeroDivineShieldProof.NewGame(history);
            var recruit = HeroDivineShieldProof.Summon(game, "CS2_101t");
            for (int i = 0; i < sources; i++) HeroDivineShieldProof.Summon(game, "CS2_222");
            Generic.AddEnchantmentBlock(game, Cards.FromId("CS2_092e"), game.Player1.Hero, recruit);
            recruit.TakeDamage(game.Player2.Hero, damage);
            Check(recruit.BaseHealth == 5 + sources && recruit.Damage == damage && !recruit.ToBeDestroyed,
                "Kings plus external aura fixture");
            foreach (var resumed in new[] { game, game.CloneExact(history), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var target = (Minion)resumed.IdEntityDic[recruit.Id];
                target.Silence();
                resumed.DeathProcessingAndAuraUpdate();
                int expectedDamage = damage == 5 ? 1 : 0;
                Check(target.BaseHealth == 1 + sources && target.Damage == expectedDamage
                    && target.Health == 1 + sources - expectedDamage && !target.ToBeDestroyed
                    && target.Zone.Type == Zone.PLAY,
                    $"local +4 Health removal preserves survivor: sources={sources}, damage={damage}, history={history}");
                Check(target.AttackDamage == 1 + sources
                    && !(target.AppliedEnchantments?.Any(e => e.Card.Id == "CS2_092e") ?? false),
                    "Silence removes Kings while retaining external attack/health");
                target.Silence();
                resumed.DeathProcessingAndAuraUpdate();
                Check(target.Damage == expectedDamage && target.Zone.Type == Zone.PLAY,
                    "repeated Silence cannot heal damage remaining after local buff removal");
            }
        }
    }

}
