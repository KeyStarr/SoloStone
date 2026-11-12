using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class ConvertedHealingFatigueProof
{
    [GroupProof("converted-healing-fatigue")]
    internal static void Run()
    {
        foreach (bool history in new[] { false, true })
        foreach (bool ruby in new[] { true, false })
        foreach (bool priorDraw in new[] { false, true })
        {
            var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
            foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
            if (priorDraw) Generic.Draw(p);
            Play(game, p, "BE_000");
            if (ruby) LocationProofs.Use(LocationProofs.Play(p, "CATA_301"));
            else Summon(p, "EX1_591"); // Auchenai Soulpriest's persistent conversion.
            var target = Summon(p.Opponent, "CS2_182"); target.AttackDamage = 0;
            p.Hero.Damage = 5; p.Opponent.Hero.Damage = 5;
            p.Hero.Weapon.AttackDamage = 2; p.Hero.IsExhausted = false;
            Core2026Proof.Check(game.Process(HeroAttackTask.Any(p, target)), "converted Lightcaller attack accepted");
            Core2026Proof.Check(p.Hero.Damage == 7, $"Lightcaller converts two healing into two self-damage: got {p.Hero.Damage}");
            p.Hero.RemoveWeapon();
            int before = p.Hero.Damage;
            Generic.Draw(p);
            int expected = priorDraw ? 2 : 1;
            Core2026Proof.Check(p.Hero.Damage - before == expected,
                $"converted heal must not advance fatigue: expected next draw {expected}, got {p.Hero.Damage - before} (history={history}, Ruby={ruby})");
            Core2026Proof.Check(p.Hero.Fatigue == expected, "only empty-deck draws advance fatigue counter");
        }
        foreach (bool history in new[] { false, true })
        foreach (string sourceCard in new[] { "HERO", "CS1_130", "CS2_231" })
        {
            var game = HeroDivineShieldProof.NewGame(history); var p = game.Player1;
            IPlayable source = sourceCard == "HERO" ? p.Hero
                : sourceCard == "CS1_130" ? Add(p, sourceCard) : Summon(p, sourceCard);
            p.RestoreToDamage = true;
            p.Hero.TakeHeal(source, 2);
            Core2026Proof.Check(p.Hero.Damage == 2 && p.Hero.Fatigue == 0, "converted healing never writes fatigue, regardless of source");
            p.Hero.TakeDamage(p.Hero, 3);
            Core2026Proof.Check(p.Hero.Damage == 5 && p.Hero.Fatigue == 0, "ordinary hero self-damage never writes fatigue");
            foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
            p.Hero.IsImmune = true; Generic.Draw(p);
            Core2026Proof.Check(p.Hero.Damage == 5 && p.Hero.Fatigue == 1, "prevented fatigue still advances draw counter");
            p.Hero.IsImmune = false; Generic.Draw(p);
            Core2026Proof.Check(p.Hero.Damage == 7 && p.Hero.Fatigue == 2, "second draw deals two after prevented first fatigue");
        }
        Console.WriteLine("PASS converted healing/fatigue: Lightcaller + Ruby/Auchenai, prior draw, hero/spell/minion source, prevention, history on/off");
    }
}
