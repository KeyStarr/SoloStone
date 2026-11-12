using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;
using L = LocationProofs;

internal static class SanguineDepthsProof
{
    internal static void StandardCase() => StandardSetsProof.Case("CORE_REV_990", (game, p, id) => {
        var target = L.Minion(p);
        int attack = target.AttackDamage, health = target.Health;
        var depths = L.Play(p, id);
        L.Use(depths, target);
        L.Check(target.Health == health - 1 && target.AttackDamage == attack + 2, "Depths deals 1 damage and grants +2 Attack");
        L.Check(game.IdEntityDic.Values.OfType<Enchantment>().Any(e => e.Card.Id == "REV_990e" && e.Target == target), "Depths applies REV_990e to target");
        L.End(game);
        L.Check(game.Step == Step.MAIN_ACTION && game.DeadBoardObjects.Count == 0, "Depths reaches snapshot boundary");
    });

    internal static void Run()
    {
        for (int seed = 1; seed <= 3; seed++)
        foreach (bool history in new[] { false, true })
        foreach (bool enemy in new[] { false, true })
        foreach (bool lethal in new[] { false, true })
        {
            var game = L.Game(seed, history); var p = game.Player1;
            var target = L.Minion(enemy ? p.Opponent : p, lethal ? "CS2_231" : "CS2_182");
            int attack = target.AttackDamage, health = target.Health;
            var depths = L.Play(p, "CORE_REV_990");
            L.Use(depths, target);
            L.Check(target.AttackDamage == attack + 2 && target.Health == health - 1, "Depths +2 Attack then damage");

            if (history) DepthsBuffBeforeDamage(game, target);
            if (lethal) L.Check(target.Zone == target.Controller.GraveyardZone && target.AttackDamage == target.Card.ATK + 2,
                "lethal damage retains +2 Attack with History on or off");
            else L.Check(target.Zone == target.Controller.BoardZone, "surviving target stays on board");
            L.Check(!LocationRules.CanActivate(p, depths, target) && depths.Durability == 2, "spent charge cannot repeat");
        }
        Console.WriteLine("PASS Sanguine Depths seeds 1-3 friendly/enemy/lethal/surviving; DepthsBuffBeforeDamage");
    }
    private static void DepthsBuffBeforeDamage(SabberStoneCore.Model.Game game, Minion target)
    {
        // Pinned: T2 friendly_etc_played_twice.195635, block 363 and L15925-15990 (lethal Tendril).
        var entries = game.PowerHistory.Last.ToList();
        int buff = entries.FindIndex(e => e is PowerHistoryFullEntity f && game.IdEntityDic[f.Entity.Id] is Enchantment enchantment &&
            enchantment.Card.Id == "REV_990e" && enchantment.Target == target);
        int damage = entries.FindIndex(e => e is PowerHistoryTagChange t && t.EntityId == target.Id && t.Tag == GameTag.DAMAGE);
        L.Check(buff >= 0 && damage > buff, "DepthsBuffBeforeDamage REV_990e created before target DAMAGE");
    }
}
