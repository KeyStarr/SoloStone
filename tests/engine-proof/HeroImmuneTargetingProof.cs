using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HeroImmuneTargetingProof
{
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        foreach (bool aura in new[] { false, true })
        {
            var game = HeroDivineShieldProof.NewGame(history);
            var owner = game.Player1;
            var hero = owner.Hero;
            var source = Summon(owner, "CS2_231");
            if (aura) new Aura(AuraType.HERO, Effects.Immune).Activate(source);
            else hero.IsImmune = true;
            game.AuraUpdate();
            if (aura)
            {
                var other = Summon(owner, "CS2_231");
                new Aura(AuraType.HERO, Effects.Immune).Activate(other);
                game.AuraUpdate();
                other.Silence(); game.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(hero.IsImmune && hero[GameTag.IMMUNE] == 1,
                    "removing one Immune aura must retain another source");
            }
            Core2026Proof.Check(hero.IsImmune && hero[GameTag.IMMUNE] == 1, "hero exposes derived IMMUNE for raw and aura immunity");
            hero.Damage = 6;
            Play(game, owner, "CS2_089", hero);
            Core2026Proof.Check(hero.Damage == 0, "owner can target and heal own Immune hero");
            hero.TakeDamage(owner.Opponent.Hero, 5);
            Core2026Proof.Check(hero.Damage == 0, "Immune hero prevents damage");
            End(game);
            var enemy = owner.Opponent;
            enemy.BaseMana = 10;
            foreach (string id in new[] { "CS2_029", "CS2_037", "EX1_561" })
            {
                var card = Add(enemy, id);
                Core2026Proof.Check(!card.Card.GetValidPlayTargets(enemy).Contains(hero)
                    && !card.Card.TargetingRequirements(enemy, hero), "validated targets reject enemy Immune hero: " + id);
                Core2026Proof.Check(!enemy.Options().Any(t => t.Source?.Id == card.Id && t.Target?.Id == hero.Id),
                    "Options exposes enemy Immune hero: " + id);
                Core2026Proof.Check(!game.Process(PlayCardTask.Any(enemy, card, hero)) && card.Zone == enemy.HandZone,
                    "explicit hostile targeting rejected without playing card: " + id);
            }
            Core2026Proof.Check(!enemy.Options().Any(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER && t.Target?.Id == hero.Id),
                "hero power options reject enemy Immune hero");
            var attacker = Summon(enemy, "CS2_182");
            attacker.IsExhausted = false;
            enemy.Hero.AttackDamage = 2;
            Core2026Proof.Check(!attacker.ValidAttackTargets.Contains(hero) && !enemy.Hero.ValidAttackTargets.Contains(hero)
                && !enemy.Options().Any(t => t.Target?.Id == hero.Id), "attacks and all hostile options reject Immune hero");
            if (aura) { source.Silence(); game.DeathProcessingAndAuraUpdate(); }
            else hero.IsImmune = false;
            Core2026Proof.Check(!hero.IsImmune && hero[GameTag.IMMUNE] == 0
                && enemy.Options().Any(t => t.Source?.Card.Id == "CS2_029" && t.Target?.Id == hero.Id),
                "removing immunity restores visible tag and hostile targeting");
        }
        Console.WriteLine("PASS hero Immune targeting: raw/aura, friendly healing, hostile spell/power/battlecry/attacks, removal, history on/off");
    }
}
