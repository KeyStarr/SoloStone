using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class SilenceKeywordsProof
{
    internal static bool CanAttack(Minion minion, ICharacter target) => minion.Controller.Options().Any(t =>
        t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == minion.Id && t.Target?.Id == target.Id);

    internal static void Run()
    {
        foreach (bool history in new[] { true, false })
        for (int seed = 1; seed <= 3; seed++)
        foreach (string kind in new[] { "enchantment Rush", "effect Rush", "printed Rush", "enchantment Charge", "printed Charge" })
        foreach (bool older in new[] { false, true })
        {
            var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, SkipMulligan = true,
                History = history, Logging = false, FormatType = FormatType.FT_WILD });
            game.StartGame();
            var player = game.Player1;
            var enemy = Summon(player.Opponent, "CS2_182");
            string id = kind == "printed Rush" ? "GIL_113" : kind == "printed Charge" ? "CS2_173" : "CS2_172";
            var minion = (Minion)Play(game, player, id);
            if (older) { End(game); End(game); }
            if (kind.StartsWith("enchantment"))
                new EffectContext(game, player.Id, minion.Id).GiveBuff(minion.Id,
                    kind == "enchantment Rush" ? "DAL_714e" : "CS2_103e2");
            if (kind == "effect Rush") new Effect(GameTag.RUSH, EffectOperator.SET, 1).ApplyTo(minion);
            Core2026Proof.Check(CanAttack(minion, enemy), kind + " permits minion attack before silence");
            if (!older && kind.Contains("Rush")) Core2026Proof.Check(!CanAttack(minion, player.Opponent.Hero), "fresh Rush cannot hit hero");
            foreach (var copy in new[] { game, game.CloneExact(history: history), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var target = (Minion)copy.IdEntityDic[minion.Id];
                var opponent = (Minion)copy.IdEntityDic[enemy.Id];
                Play(copy, copy.Player1, "BE_228", target);
                Core2026Proof.Check(target.IsSilenced && !target.IsRush && !target.HasCharge && !target.AttackableByRush,
                    kind + " silence removes keyword and Rush restriction");
                Core2026Proof.Check(CanAttack(target, opponent) == older && CanAttack(target, copy.Player2.Hero) == older,
                    kind + " silence restores summoning sickness only on fresh minions");
                Core2026Proof.Check(target.AttackDamage == target.Card.ATK && target.Health == target.Card.Health,
                    "silence removes enchantment stats");
                End(copy); End(copy);
                Core2026Proof.Check(CanAttack(target, opponent) && CanAttack(target, copy.Player2.Hero), "silenced minion ready next turn");
            }
        }
        RunReadinessRegressions();
        Console.WriteLine("PASS silence keywords: printed/enchantment/effect Rush, printed/enchantment Charge; fresh/older; seeds=3; history on/off; clone/snapshot");
    }

    private static void RunReadinessRegressions()
    {
        var failures = new List<string>();
        foreach (bool history in new[] { true, false })
        foreach (string scenario in new[] { "Rhino silence", "Rhino death", "Shaw death", "Charge to Rush", "Rush to Charge",
            "in-place transform", "Windfury silence", "Windfury effect removal", "Charge effect removal", "Rush effect removal",
            "Windfury property removal", "Charge property removal", "Rush property removal",
            "Windfury effect zero", "Charge effect zero", "Rush effect zero" })
        foreach (bool older in new[] { false, true })
        {
            var game = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, SkipMulligan = true,
                History = history, Logging = false, FormatType = FormatType.FT_WILD });
            game.StartGame();
            var player = game.Player1;
            var enemy = Summon(player.Opponent, "CS2_173");
            Minion aura = null;
            if (scenario is "Rhino death" or "Charge to Rush") aura = (Minion)Play(game, player, "DS1_178");
            if (scenario is "Shaw death" or "Rush to Charge" || scenario.StartsWith("Windfury") && !older)
                aura = (Minion)Play(game, player, "GIL_650");
            if (scenario == "Charge to Rush") Play(game, player, "GIL_650");
            string card = scenario == "Rhino silence" ? "DS1_178" :
                scenario == "Windfury silence" ? "EX1_033" : // Windfury Harpy survives the attack.
                scenario.StartsWith("Windfury") ? "CS2_182" :
                scenario is "Rhino death" or "Charge to Rush" ? "CS2_172" :
                scenario == "Rush to Charge" ? "CS2_173" : "CS2_231";
            var minion = (Minion)Play(game, player, card);
            if (older || scenario == "in-place transform") { End(game); End(game); }
            if (scenario == "in-place transform")
                Core2026Proof.Check(ReferenceEquals(minion, Generic.ChangeEntityBlock(player, minion, Cards.FromId("CS2_173"), true)),
                    "transform uses the in-place Evolve path");
            GameTag keyword = scenario.StartsWith("Windfury") ? GameTag.WINDFURY :
                scenario.StartsWith("Rush") ? GameTag.RUSH : GameTag.CHARGE;
            if (scenario.Contains("effect") || scenario.Contains("property"))
                new Effect(keyword, EffectOperator.SET, 1).ApplyTo(minion);
            if (scenario.StartsWith("Windfury"))
            {
                // Use legal options without the shared Attack helper's forced readiness.
                var attack = player.Options().Single(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK
                    && t.Source?.Id == minion.Id && t.Target?.Id == enemy.Id);
                Core2026Proof.Check(game.Process(attack) && minion.NumAttacksThisTurn == 1, "first Windfury attack accepted");
                enemy = Summon(player.Opponent, "CS2_173");
            }
            Core2026Proof.Check(CanAttack(minion, enemy), scenario + " ready before keyword loss");
            int copyIndex = 0;
            foreach (var copy in new[] { game, game.CloneExact(history: history), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                string label = $"{scenario}, older={older}, history={history}, copy={copyIndex++}";
                try
                {
                    var target = (Minion)copy.IdEntityDic[minion.Id];
                    var opponent = (Minion)copy.IdEntityDic[enemy.Id];
                    if (scenario is "Rhino death" or "Shaw death" or "Charge to Rush" or "Rush to Charge")
                        Kill(copy, (Minion)copy.IdEntityDic[aura.Id]);
                    else if (scenario.EndsWith("effect removal"))
                        new Effect(keyword, EffectOperator.SET, 1).RemoveFrom(target);
                    else if (scenario.EndsWith("effect zero"))
                        new Effect(keyword, EffectOperator.SET, 0).ApplyTo(target);
                    else if (scenario.EndsWith("property removal"))
                    {
                        if (keyword == GameTag.CHARGE) target.HasCharge = false;
                        else if (keyword == GameTag.RUSH) target.IsRush = false;
                        else target.HasWindfury = false;
                    }
                    else
                        Play(copy, copy.Player1, "BE_228", target);
                    bool ready = scenario is "Charge to Rush" or "Rush to Charge" ||
                        older && scenario != "in-place transform" && !scenario.StartsWith("Windfury");
                    Core2026Proof.Check(CanAttack(target, opponent) == ready, "minion attack readiness after keyword loss");
                    Core2026Proof.Check(CanAttack(target, copy.Player2.Hero) == (ready && (older || scenario != "Charge to Rush")),
                        "hero attack readiness after keyword loss");
                    if (!ready) Core2026Proof.Check(target.IsExhausted && !target.CanAttack, "exhaustion agrees with legal options");
                    End(copy); End(copy);
                    Core2026Proof.Check(CanAttack(target, opponent) && CanAttack(target, copy.Player2.Hero), "ready again next turn");
                }
                catch (Exception ex) { failures.Add(label + ": " + ex.Message); }
            }
        }
        Core2026Proof.Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine("PASS readiness regressions: aura loss, Charge/Rush overlap, in-place transform, Windfury limit; fresh/older; history on/off; clone/snapshot");
    }

}
