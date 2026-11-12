using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class WeaponKeywordProof
{
    internal static void Run()
    {
        int cases = 0;
        foreach (bool history in new[] { false, true })
        {
            foreach (int attacks in new[] { 1, 2 })
            {
                var game = HeroDivineShieldProof.NewGame(history);
                var target = Summon(game.Player2, "EX1_572");
                Play(game, game.Player1, "KAR_028");
                for (int attack = 0; attack < attacks; attack++)
                    Core2026Proof.Check(game.Process(HeroAttackTask.Any(game.Player1, target)), "Fool's Bane attack before plain replacement accepted");
                Core2026Proof.Check(game.Player1.Hero.CanAttack && Offered(game), "Fool's Bane still ready before plain replacement");
                Play(game, game.Player1, "CS2_106");
                Core2026Proof.Check(game.Player1.Hero.IsExhausted, "plain replacement exhausts hero after Fool's Bane attacks: " + attacks);
                RejectAttack(game, target);
                cases++;
            }
            {
                var game = HeroDivineShieldProof.NewGame(history);
                var target = Summon(game.Player2, "EX1_572"); // Ysera survives all three Bane hits.
                Play(game, game.Player1, "EX1_567");
                Attack(game);
                Play(game, game.Player1, "KAR_028");
                var hero = game.Player1.Hero;
                for (int attack = 1; attack <= 3; attack++)
                {
                    Core2026Proof.Check(hero.CanAttack && Offered(game), "Doomhammer replacement with Fool's Bane offers minion attack " + attack);
                    Core2026Proof.Check(game.Process(HeroAttackTask.Any(game.Player1, target)), "Fool's Bane attack after Doomhammer accepted");
                    Core2026Proof.Check(hero.NumAttacksThisTurn == 1 + attack && hero.Weapon.Durability == 4 - attack
                        && target.Health == 12 - 3 * attack, "Fool's Bane attacks after Doomhammer spend durability and damage minion");
                }
                cases++;
            }
            {
                var game = HeroDivineShieldProof.NewGame(history);
                var target = Summon(game.Player2, "EX1_572"); // Ysera survives all three hits.
                Play(game, game.Player1, "KAR_028");
                var hero = game.Player1.Hero;
                for (int attack = 1; attack <= 3; attack++)
                {
                    Core2026Proof.Check(hero.CanAttack && Offered(game), "Fool's Bane offers consecutive minion attacks");
                    Core2026Proof.Check(game.Process(HeroAttackTask.Any(game.Player1, target)), "Fool's Bane attack accepted");
                    Core2026Proof.Check(hero.NumAttacksThisTurn == attack && hero.Weapon.Durability == 4 - attack,
                        "Fool's Bane consecutive attacks spend durability");
                }
                Core2026Proof.Check(target.Health == 3 && target.Zone == game.Player2.BoardZone && hero.CanAttack && Offered(game),
                    "Fool's Bane remains ready while durability and targets remain");
                cases++;
            }
            foreach (string unchanged in new[] { "hero", "weapon", "equip" })
            {
                var game = HeroDivineShieldProof.NewGame(history);
                Play(game, game.Player1, "CS2_106");
                var hero = game.Player1.Hero;
                Attack(game);
                hero.IsExhausted = false;
                if (unchanged == "hero") hero.HasWindfury = false;
                else if (unchanged == "weapon") hero.Weapon.IsWindfury = false;
                else Play(game, game.Player1, "CS2_106");
                Core2026Proof.Check(hero.CanAttack && Offered(game), "unchanged Windfury preserves explicit readiness: " + unchanged);
                Attack(game);
                RejectAttack(game);
                cases++;
            }
            foreach (string change in new[] { "weapon-grant", "equip", "hero-loss", "weapon-loss", "destroy", "replace" })
            {
                var game = HeroDivineShieldProof.NewGame(history);
                var hero = game.Player1.Hero;
                Play(game, game.Player1, "CS2_106");
                bool gaining = change is "weapon-grant" or "equip";
                if (!gaining)
                {
                    if (change == "hero-loss") hero[GameTag.WINDFURY] = 1;
                    else hero.Weapon[GameTag.WINDFURY] = 1;
                }
                Attack(game);
                switch (change)
                {
                    case "weapon-grant": hero.Weapon[GameTag.WINDFURY] = 1; break;
                    case "equip": Play(game, game.Player1, "EX1_567"); break;
                    case "hero-loss": hero[GameTag.WINDFURY] = 0; break;
                    case "weapon-loss": hero.Weapon[GameTag.WINDFURY] = 0; break;
                    case "destroy": hero.AttackDamage = 2; hero.Weapon.ToBeDestroyed = true;
                        game.DeathProcessingAndAuraUpdate(); break;
                    case "replace": Play(game, game.Player1, "CS2_106"); break;
                }
                Core2026Proof.Check(hero.IsExhausted == !gaining && hero.CanAttack == gaining && Offered(game) == gaining,
                    "Windfury transition reconciles readiness: " + change);
                if (gaining) Attack(game);
                RejectAttack(game);
                cases++;
            }
            foreach (string unchanged in new[] { "hero", "weapon", "equip", "unequipped-weapon", "other-source" })
            {
                var game = HeroDivineShieldProof.NewGame(history);
                Play(game, game.Player1, "EX1_567");
                var hero = game.Player1.Hero;
                Attack(game);
                hero.IsExhausted = true;
                switch (unchanged)
                {
                    case "hero": hero.HasWindfury = true; break;
                    case "weapon": hero.Weapon.IsWindfury = true; break;
                    case "equip": Play(game, game.Player1, "EX1_567"); break;
                    case "unequipped-weapon": ((Weapon)Add(game.Player1, "CS2_106")).IsWindfury = true; break;
                    case "other-source": hero.HasWindfury = true; hero.Weapon.IsWindfury = false; break;
                }
                RejectAttack(game);
                cases++;
            }
            {
                var game = HeroDivineShieldProof.NewGame(history);
                Play(game, game.Player1, "EX1_567");
                Attack(game);
                Play(game, game.Player1, "EX1_567");
                Core2026Proof.Check(game.Player1.Hero.CanAttack && Offered(game), "Windfury replacement preserves ready second attack");
                Attack(game);
                RejectAttack(game);
                cases++;
            }
            foreach (bool silenced in new[] { false, true })
            {
                var game = HeroDivineShieldProof.NewGame(history);
                Play(game, game.Player1, "EX1_567"); // Doomhammer: printed Windfury.
                var hero = game.Player1.Hero;
                Core2026Proof.Check(hero.Weapon.IsWindfury && hero.HasWindfury, "printed Doomhammer Windfury survives creation");
                if (silenced)
                {
                    hero.Weapon[GameTag.SILENCED] = 1;
                    hero.Weapon[GameTag.WINDFURY] = 0;
                }
                Attack(game);
                Core2026Proof.Check(hero.CanAttack == !silenced, "Doomhammer attack allowance uses effective Windfury");
                Core2026Proof.Check(Offered(game) == !silenced, "Doomhammer attack options use effective Windfury");
                if (!silenced) Attack(game);
                RejectAttack(game);
                cases++;
            }
            foreach (bool loseAfterAttack in new[] { false, true })
            {
                var game = HeroDivineShieldProof.NewGame(history);
                Play(game, game.Player1, "CS2_106"); // No printed Windfury.
                var weapon = game.Player1.Hero.Weapon;
                weapon.IsWindfury = true;
                Core2026Proof.Check(weapon.IsWindfury && game.Player1.Hero.HasWindfury, "weapon Windfury grant is effective");
                if (loseAfterAttack) Attack(game);
                weapon.IsWindfury = false;
                if (!loseAfterAttack) Attack(game);
                RejectAttack(game);
                cases++;
            }
            foreach (bool equipped in new[] { false, true })
            {
                var game = HeroDivineShieldProof.NewGame(history);
                var hero = game.Player1.Hero;
                if (equipped) Play(game, game.Player1, "CS2_106");
                else hero.AttackDamage = 2;
                Attack(game);
                hero.HasWindfury = true;
                Core2026Proof.Check(hero[GameTag.WINDFURY] == 1 && (hero.Weapon == null || !hero.Weapon.IsWindfury),
                    "hero Windfury setter grants hero tag without modifying weapon");
                Core2026Proof.Check(hero.CanAttack && Offered(game), "direct hero Windfury grant after attack restores second attack");
                Attack(game);
                RejectAttack(game);
                cases++;
            }
            foreach (bool directHero in new[] { false, true })
            {
                var game = HeroDivineShieldProof.NewGame(history);
                Play(game, game.Player1, "EX1_567");
                var hero = game.Player1.Hero;
                hero.Damage = 10;
                hero.Weapon.HasLifeSteal = true;
                Attack(game);
                Core2026Proof.Check(hero.Damage == 8, "effective weapon Lifesteal heals actual hero attack damage");
                hero.Weapon.HasLifeSteal = false;
                Core2026Proof.Check(!hero.Weapon.HasLifeSteal, "weapon Lifesteal zero disables healing");
                if (directHero) hero[GameTag.LIFESTEAL] = 1;
                Attack(game);
                Core2026Proof.Check(hero.Damage == (directHero ? 6 : 8), "hero-or-weapon effective Lifesteal controls healing");
                cases++;
            }
            // Existing card-granted extra attacks remain available without Windfury.
            {
                var game = HeroDivineShieldProof.NewGame(history);
                game.Player1.Hero.AttackDamage = 2;
                Attack(game);
                game.Player1.Hero.ExtraAttacksThisTurn = 1;
                Core2026Proof.Check(Offered(game), "extra hero attack remains offered");
                Attack(game);
                RejectAttack(game);
                cases++;
            }
        }
        Console.WriteLine($"PASS weapon/hero effective keywords: {cases} cases, history on/off");
    }

    private static bool Offered(Game game) => game.Player1.Options().Any(task =>
        task.PlayerTaskType == PlayerTaskType.HERO_ATTACK);

    private static void Attack(Game game)
    {
        Core2026Proof.Check(game.Process(HeroAttackTask.Any(game.Player1, game.Player2.Hero)), "hero attack accepted");
    }

    private static void RejectAttack(Game game, ICharacter target = null)
    {
        target ??= game.Player2.Hero;
        var hero = game.Player1.Hero;
        int damage = target.Damage, attacks = hero.NumAttacksThisTurn;
        Core2026Proof.Check(!hero.CanAttack && !Offered(game), "spent hero attack allowance must not be offered");
        Core2026Proof.Check(!game.Process(HeroAttackTask.Any(game.Player1, target))
            && target.Damage == damage && hero.NumAttacksThisTurn == attacks,
            "attack beyond effective allowance rejected without damage");
    }
}
