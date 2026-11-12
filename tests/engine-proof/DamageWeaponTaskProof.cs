using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

// Regression test. DamageWeaponTask used to clamp its configured amount in place when a weapon had little durability
// left. The task object belongs to the card and is shared across games, clones and replays, so one short weapon
// permanently lowered the damage of every later use. This showed up as a fuzz replay divergence with stress decks.
internal static class DamageWeaponTaskProof
{
    internal static void Run()
    {
        foreach (bool opponent in new[] { false, true })
        foreach (var (amount, remaining) in new[] { (1, 0), (3, 1) })
        {
            var game = HeroDivineShieldProof.NewGame(history: true);
            Play(game, game.Player1, "EX1_366");
            var clone = game.CloneExact(history: true);
            var task = new DamageWeaponTask(opponent, amount);
            var source = opponent ? clone.Player2.Hero : clone.Player1.Hero;
            var weapon = clone.Player1.Hero.Weapon;
            weapon.Damage = 5 - remaining;
            task.Process(clone, source.Controller, source, null);
            Core2026Proof.Check(weapon.Durability == 0, "weapon damage clamps to remaining durability");

            source = opponent ? game.Player2.Hero : game.Player1.Hero;
            task.Process(game, source.Controller, source, null);
            Core2026Proof.Check(game.Player1.Hero.Weapon.Durability == 5 - amount,
                "DamageWeaponTask retains configured amount after exhausted/short clone weapon");

            var replay = HeroDivineShieldProof.NewGame(history: true);
            Play(replay, replay.Player1, "EX1_366");
            source = opponent ? replay.Player2.Hero : replay.Player1.Hero;
            task.Process(replay, source.Controller, source, null);
            Core2026Proof.Check(replay.Player1.Hero.Weapon.Durability == 5 - amount,
                "reusing weapon task in another game preserves damage");
        }
        Console.WriteLine("PASS weapon damage task: exhausted/short durability, clone and cross-game reuse, both owners");
    }
}
