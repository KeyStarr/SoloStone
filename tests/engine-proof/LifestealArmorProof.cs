using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// Lifesteal restores the damage dealt: armor lost plus health lost, fixed when the hit resolves.
// Armor part is a lean, not a pin: data/rules-evidence/keywords/lifesteal-armor-damage.yaml.
internal static class LifestealArmorProof
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("LifestealArmor: " + message);
    }

    private static (Game game, Hero own, Hero enemy, Minion source) Setup(bool history, int ownDamage, int enemyArmor)
    {
        var game = HeroDivineShieldProof.NewGame(history);
        var source = HeroDivineShieldProof.Summon(game, "CS2_231");
        source.HasLifeSteal = true;
        game.Player1.Hero.Damage = ownDamage;
        game.Player2.Hero.Armor = enemyArmor;
        return (game, game.Player1.Hero, game.Player2.Hero, source);
    }

    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        {
            var (game, own, enemy, source) = Setup(history, 10, 0);
            Check(enemy.TakeDamage(source, 3) == 3 && enemy.Damage == 3 && own.Damage == 7, "health-only hit heals 3");

            (game, own, enemy, source) = Setup(history, 10, 5);
            enemy.TakeDamage(source, 3);
            Check(enemy.Armor == 2 && enemy.Damage == 0 && own.Damage == 7, "Lean_LifestealArmorDamage armor-only hit heals 3");

            (game, own, enemy, source) = Setup(history, 10, 2);
            enemy.TakeDamage(source, 5);
            Check(enemy.Armor == 0 && enemy.Damage == 3 && own.Damage == 5, "Lean_LifestealArmorDamage mixed armor/health hit heals 5");

            (game, own, enemy, source) = Setup(history, 10, 5);
            enemy[GameTag.DIVINE_SHIELD] = 1;
            enemy.TakeDamage(source, 3);
            Check(enemy.Armor == 5 && enemy[GameTag.DIVINE_SHIELD] == 0 && own.Damage == 10,
                "Divine Shield prevents armor loss and Lifesteal");
            enemy.IsImmune = true;
            enemy.TakeDamage(source, 3);
            Check(enemy.Armor == 5 && own.Damage == 10, "Immune prevents armor loss and Lifesteal");

            (game, own, enemy, source) = Setup(history, 2, 10);
            enemy.TakeDamage(source, 5);
            Check(enemy.Armor == 5 && own.Damage == 0 && own.Health == own.BaseHealth, "Lean_LifestealArmorDamage heal caps at full health");

            (game, own, enemy, source) = Setup(history, 20, 2);
            enemy.Damage = 27;
            enemy.TakeDamage(source, 10);
            Check(enemy.Armor == 0 && enemy.Damage == 35 && own.Damage == 10, "Lean_LifestealArmorDamage overkill on armored hero heals full 10");
            (game, own, enemy, source) = Setup(history, 10, 0);
            var victim = (Minion)Entity.FromCard(game.Player2, Cards.FromId("CS2_231"));
            SabberStoneCore.Actions.Generic.SummonBlock(game, victim, -1, null);
            victim.TakeDamage(source, 5);
            Check(own.Damage == 5, "overkill on 1-Health minion heals full 5");

            // Healing-does-damage keeps the recursion guard: the redirected hit is not Lifesteal again.
            (game, own, enemy, source) = Setup(history, 0, 5);
            game.Player1.RestoreToDamage = true;
            int historyBefore = history ? game.PowerHistory.Full.Count : 0;
            enemy.TakeDamage(source, 3);
            Check(enemy.Armor == 2 && own.Damage == 3, "healing-does-damage redirects the armor-only amount once");
            // The TakeHeal guard alone also stops a second heal; count Lifesteal blocks, excluding inline source attribution.
            Check(!history || game.PowerHistory.Full.Skip(historyBefore).Count(e => e is PowerHistoryBlockStart b
                && b.BlockType == BlockType.TRIGGER && b.Source == source.Id
                && b.TriggerKeyword == (int)GameTag.LIFESTEAL) == 1, "redirected hit does not Lifesteal again");

            // Real Lifesteal card through a native attack: Swamp Leech (GIL_558, 2/1 Lifesteal).
            foreach (int armor in new[] { 0, 1, 5 })
            {
                game = HeroDivineShieldProof.NewGame(history);
                own = game.Player1.Hero;
                enemy = game.Player2.Hero;
                var leech = HeroDivineShieldProof.Summon(game, "GIL_558");
                Check(leech.HasLifeSteal, "Swamp Leech has Lifesteal");
                leech.IsExhausted = false;
                own.Damage = 10;
                enemy.Armor = armor;
                Check(game.Process(MinionAttackTask.Any(game.Player1, leech, enemy)), "Swamp Leech attack accepted");
                Check(enemy.Armor == Math.Max(0, armor - 2) && enemy.Damage == Math.Max(0, 2 - armor) && own.Damage == 8,
                    $"Lean_LifestealArmorDamage Swamp Leech into {armor} armor heals 2");
            }
        }
        Console.WriteLine("PASS Lifesteal armor: health, armor, mixed, Divine Shield/Immune, full-health cap, overkill, healing-does-damage, Swamp Leech attack, history on/off");
    }
}
