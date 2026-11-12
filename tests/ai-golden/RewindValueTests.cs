using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class RewindValueTests
{
    public static void TestStadiumPricesVisibleWeaponStats()
    {
        foreach (string ownCard in new[] { "BE_000", "JAIL_376" })
        foreach (int durability in new[] { 3, 1 }) foreach (bool enemyBuffed in new[] { false, true }) {
            var game = Fixture(CardClass.WARRIOR, CardClass.WARRIOR);
            var announcer = Entity.FromCard(game.Player1, Cards.FromId("TIME_034"), zone: game.Player1.HandZone);
            Check(game.Process(game.Player1.Options().First(t => t.Source?.Id == announcer.Id)), "Stadium Rewind offer");
            bool lightcaller = ownCard == "BE_000";
            var own = (Weapon)Entity.FromCard(game.Player1, Cards.FromId(ownCard));
            game.Player1.Hero.AddWeapon(own);
            own.AttackDamage = lightcaller ? 5 : 2; own.Durability = durability;
            var enemy = (Weapon)Entity.FromCard(game.Player2, Cards.FromId(lightcaller ? "RLK_067" : "EDR_525"));
            game.Player2.Hero.AddWeapon(enemy);
            enemy.AttackDamage = lightcaller || enemyBuffed ? 5 : 1;
            enemy.Durability = enemyBuffed ? 5 : lightcaller ? 2 : 3;
            foreach (var variant in new[] { game, Variant(game) }) {
                var pilot = new GoldenPilot(NpcCatalog.All[0]); int index = 0;
                var chosen = BotDriver.Decide(variant, 1, DeckStyle.Tempo,
                    s => { throw new Exception("Rewind entered search"); }, ref index, pilot.Options);
                bool keep = lightcaller && durability == 3 && !enemyBuffed;
                Check(chosen.Trace.Kind == "rewind" && ((ChooseTask)chosen.Task).Choices.Single()
                    == (keep ? variant.Rewind.KeepEntityId : variant.Rewind.RewindEntityId),
                    "price visible weapon stats against the buffed pool, including durability and enemy buffs");
            }
        }
    }
}
