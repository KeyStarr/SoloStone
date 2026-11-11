using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using L = LocationProofs;

internal static class PastGnomereganProof
{
    internal static void StandardCase() => StandardSetsProof.Case("TIME_044", (game, p, id) => {
        var target = L.Minion(p); var source = L.Play(p, id);
        L.Use(source, target);
        L.Check(target.AttackDamage == 6 && target.Health == 6 && source.Card.Id == "TIME_044t1" && source.Durability == 2, "Past buffs and advances");
        L.End(game);
    });
    internal static void Run()
    {
        for (int seed = 1; seed <= 3; seed++)
        foreach (bool enemy in new[] { false, true })
        foreach (int stage in new[] { 0, 1, 2 })
        {
            var game = L.Game(seed); var p = game.Player1;
            var target = L.Minion(enemy ? p.Opponent : p);
            string id = stage == 0 ? "TIME_044" : "TIME_044t" + stage;
            var location = L.Location(p, id); location[GameTag.DAMAGE] = location[GameTag.HEALTH] - 1;
            game.DeathProcessingAndAuraUpdate();
            foreach (var fork in new[] { game, game.CloneExact(history:true), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var source = (BoardObject)fork.IdEntityDic[location.Id]; var buffed = (Minion)fork.IdEntityDic[target.Id];
                L.Use(source, buffed);
                L.Check(buffed.AttackDamage == 6 && buffed.Health == 6 && buffed.AppliedEnchantments.Any(e => e.Card.Id == "TIME_044e"), "each stage grants +2/+1 enchantment");
                L.Check(buffed.AppliedEnchantments.Any(e => e.Card.Id == "TIME_044t1e") == (stage > 0), "Deathrattle enchantment only present/future");
                L.Check(buffed.HasDivineShield == (stage == 2), "only future grants Divine Shield");
                L.Check(source.Zone == fork.Player1.GraveyardZone && source.Card.Id == id && source.Location.Stage == 0, "final charge retires without advance");
                int victimHealth = buffed.Controller.Opponent.Hero.Health, ownerHealth = buffed.Controller.Hero.Health;
                buffed.Destroy(); fork.DeathProcessingAndAuraUpdate();
                L.Check(buffed.Controller.Opponent.Hero.Health == victimHealth - (stage > 0 ? 2 : 0) && buffed.Controller.Hero.Health == ownerHealth, "granted Deathrattle follows buffed minion controller");
            }
        }
        Console.WriteLine("PASS Gnomeregan seeds 1-3 stages/enchantments/friendly/enemy/Deathrattle/Divine Shield/final-charge/clone/snapshot");
    }
}
