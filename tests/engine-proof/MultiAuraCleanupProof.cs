using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enums;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class MultiAuraCleanupProof
{
    internal static void Run()
    {
        HeroReplacement();
        var failures = new List<string>();
        void Verify(bool condition, string message)
        {
            if (!condition) failures.Add(message);
        }

        void Removed(Game game, IPlayable source, MultiAura container, string label)
        {
            Verify(source.OngoingEffect == null && !container.On, $"{label}: source aura disabled");
            Verify(!game.Auras.Contains(container) && container.All(child => !game.Auras.Contains(child))
                && !game.Auras.Any(aura => aura.Owner.Id == source.Id),
                $"{label}: container and children unregistered");
            Verify(!game.IdEntityDic.Values.Any(entity => entity.AppliedEnchantments?.Any(
                enchantment => enchantment.Creator?.Id == source.Id) == true),
                $"{label}: no remaining enchantment created by source");
        }

        foreach (string action in new[] { "death", "silence", "bounce" })
        {
            var game = HeroDivineShieldProof.NewGame(history: true);
            var paragon = Summon(game.Player1, "GIL_685");
            foreach (int attack in new[] { 3, 2, 3 })
            {
                paragon.AttackDamage = attack;
                game.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(paragon[GameTag.TAUNT] == (attack >= 3 ? 1 : 0)
                    && paragon[GameTag.LIFESTEAL] == (attack >= 3 ? 1 : 0),
                    $"Paragon fixture: keywords follow attack={attack}");
            }

            foreach (var (mode, branch) in Branches(game))
            {
                var source = (Minion)branch.IdEntityDic[paragon.Id];
                string label = $"{mode}, Paragon {action}";
                Core2026Proof.Check(source.AttackDamage == 3 && source[GameTag.TAUNT] == 1
                    && source[GameTag.LIFESTEAL] == 1 && source.OngoingEffect is MultiAura,
                    $"{label}: active keywords and MultiAura fixture");
                var container = (MultiAura)source.OngoingEffect;
                switch (action)
                {
                    case "death": source.ToBeDestroyed = true; break;
                    case "silence": source.Silence(); break;
                    case "bounce": Core2026Proof.Check(Generic.ReturnToHandBlock(source.Controller, source), $"{label}: return accepted"); break;
                }
                branch.DeathProcessingAndAuraUpdate();
                var expectedZone = action == "death" ? Zone.GRAVEYARD : action == "bounce" ? Zone.HAND : Zone.PLAY;
                Verify(source.Zone?.Type == expectedZone && (action != "silence" || source.IsSilenced),
                    $"{label}: expected {expectedZone}, got {source.Zone?.Type}");
                Verify(source[GameTag.TAUNT] == 0 && !source.HasTaunt,
                    $"{label}: TAUNT={source[GameTag.TAUNT]}, expected 0");
                Verify(source[GameTag.LIFESTEAL] == 0 && !source.HasLifeSteal,
                    $"{label}: LIFESTEAL={source[GameTag.LIFESTEAL]}, expected 0");
                Removed(branch, source, container, label);
            }
        }

        foreach (string cardId in new[] { "LOOT_044", "CORE_LOOT_044" })
        foreach (int armor in new[] { 0, 5 })
        {
            var game = HeroDivineShieldProof.NewGame(history: true);
            game.Player1.Hero.Armor = armor;
            Core2026Proof.Check(game.Player1.Hero.Health == 30 && game.Player1.Hero.BaseHealth == 30
                && game.Player1.Hero[GameTag.HEALTH] == 30, "Gauntlet fixture: initial health 30");
            var weapon = (Weapon)Add(game.Player1, cardId);
            Core2026Proof.Check(game.Process(PlayCardTask.Any(game.Player1, weapon)), "equip Bladed Gauntlet");
            foreach (var (mode, branch) in Branches(game))
            {
                var source = (Weapon)branch.IdEntityDic[weapon.Id];
                var hero = branch.Player1.Hero;
                string label = $"{mode}, {cardId}, armor={armor}";
                Core2026Proof.Check(hero.Weapon == source && hero.AttackDamage == armor
                    && hero[GameTag.CANNOT_ATTACK_HEROES] == 1 && source.OngoingEffect is MultiAura,
                    $"{label}: attack and hero restriction fixture");
                Verify(hero.Health == 30 && hero.BaseHealth == 30 && hero[GameTag.HEALTH] == 30,
                    $"{label}, equipped: Health={hero.Health}, BaseHealth={hero.BaseHealth}, HEALTH={hero[GameTag.HEALTH]}, expected 30");
                var container = (MultiAura)source.OngoingEffect;
                source.ToBeDestroyed = true;
                branch.DeathProcessingAndAuraUpdate();
                Verify(hero.Weapon == null && source.Zone?.Type == Zone.GRAVEYARD,
                    $"{label}: weapon removed to graveyard");
                for (int pass = 0; pass < 2; pass++)
                {
                    Verify(hero.AttackDamage == 0 && hero[GameTag.ATK] == 0,
                        $"{label}, pass={pass}: hero ATK={hero[GameTag.ATK]}, expected 0");
                    Verify(hero[GameTag.CANNOT_ATTACK_HEROES] == 0,
                        $"{label}, pass={pass}: CANNOT_ATTACK_HEROES={hero[GameTag.CANNOT_ATTACK_HEROES]}, expected 0");
                    Verify(hero.Health == 30 && hero.BaseHealth == 30 && hero[GameTag.HEALTH] == 30,
                        $"{label}, destroyed, pass={pass}: Health={hero.Health}, BaseHealth={hero.BaseHealth}, HEALTH={hero[GameTag.HEALTH]}, expected 30");
                    Removed(branch, source, container, label);
                    if (pass == 0) branch.DeathProcessingAndAuraUpdate();
                }
            }
        }

        {
            var game = HeroDivineShieldProof.NewGame(history: true);
            var weapon = (Weapon)Add(game.Player1, "LOOT_044");
            Core2026Proof.Check(game.Process(PlayCardTask.Any(game.Player1, weapon)), "equip aura-slot fixture");
            new Effect(GameTag.IMMUNE, EffectOperator.SET, 1).ApplyAuraTo(game.Player1.Hero);
            new Effect(GameTag.HEROPOWER_DAMAGE, EffectOperator.SET, 2).ApplyAuraTo(game.Player1.Hero);
            new Effect(GameTag.IMMUNE, EffectOperator.SET, 1).ApplyAuraTo(weapon);
            foreach (var (mode, branch) in Branches(game))
            {
                var hero = branch.Player1.Hero;
                Verify(hero.IsImmune && hero.AuraEffects.HeroPowerDamage == 2
                    && hero[GameTag.CHARGE] == 0 && hero.AuraEffects.Charge == 0
                    && !hero.HasTaunt && !hero.AuraEffects.Taunt,
                    $"{mode}: hero immunity/power damage must not alias charge/taunt");
                Verify(hero.Weapon.AuraEffects.Immune == 1
                    && !hero.Weapon.AuraEffects.CantBeTargetedBySpells
                    && hero.Weapon[GameTag.CANT_BE_TARGETED_BY_SPELLS] == 0
                    && hero.Weapon[GameTag.CANT_BE_TARGETED_BY_HERO_POWERS] == 0,
                    $"{mode}: weapon immunity must not alias spell/hero-power targeting");
            }
        }

        Core2026Proof.Check(failures.Count == 0, "MultiAuraCleanup:\n" + string.Join("\n", failures));
        Console.WriteLine("PASS MultiAura cleanup: Paragon death/silence/bounce, Bladed Gauntlet health/restriction/destruction, aura-slot isolation, container/children/enchantments removed, original/clone/snapshot");
    }

    private static (string mode, Game game)[] Branches(Game game) => new[] {
        ("original", game), ("clone", game.CloneExact(history: true)), ("snapshot", Game.LoadSnapshot(game.SaveSnapshot()))
    };

    private static void HeroReplacement()
    {
        foreach (bool history in new[] { false, true })
        foreach (string heroCard in new[] { "ICC_827", "CORE_EX1_323" })
        {
            var game = HeroDivineShieldProof.NewGame(history);
            game.Player1.Hero.Armor = 5;
            var weapon = (Weapon)Add(game.Player1, "CORE_LOOT_044");
            Core2026Proof.Check(game.Process(PlayCardTask.Any(game.Player1, weapon)), "equip adaptive hero-replacement fixture");
            foreach (var (mode, branch) in Branches(game))
            {
                var owner = branch.Player1;
                var oldHero = owner.Hero;
                Play(branch, owner, heroCard);
                Core2026Proof.Check(owner.Hero != oldHero && owner.Hero.Armor == 10, mode + " replacement preserves Armor and adds five");
                if (heroCard == "ICC_827")
                {
                    Core2026Proof.Check(owner.Hero.Weapon?.Id == weapon.Id && owner.Hero.AttackDamage == 10
                        && owner.Hero[GameTag.CANNOT_ATTACK_HEROES] == 1, mode + " retained weapon aura follows replacement hero");
                    owner.Hero.Weapon.ToBeDestroyed = true;
                    branch.DeathProcessingAndAuraUpdate();
                }
                Core2026Proof.Check(owner.Hero.AttackDamage == (heroCard == "ICC_827" ? 0 : 3)
                    && owner.Hero[GameTag.CANNOT_ATTACK_HEROES] == 0, mode + " no stale adaptive aura after weapon removal/replacement");
            }
        }
    }
}
