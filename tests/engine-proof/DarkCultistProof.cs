using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using L = LocationProofs;

// Dark Cultist (FP1_023): "Deathrattle: Give a random friendly minion +3 Health." The random pick lands on the task stack, and
// the enchantment must read it from there. Before the fix it was given to the Deathrattle's TARGET, which is empty, so no
// minion ever got the Health.
internal static class DarkCultistProof
{
    [GroupProof("dark-cultist")]
    internal static void Run()
    {
        foreach (bool history in new[] { true, false })
        {
            var g = L.Game(history: history); var p1 = g.Player1; var p2 = g.Player2;
            var cultist = L.Minion(p1, "FP1_023"); var wisp = L.Minion(p1, "CS2_231");
            var enemy = L.Minion(p2, "CS2_231");
            p1.UsedMana = 0;
            var fireball = Entity.FromCard(p1, Cards.FromId("CS2_029"), zone: p1.HandZone);
            L.Check(g.Process(PlayCardTask.SpellTarget(p1, fireball, cultist)), "Fireball the Dark Cultist");
            L.Check(cultist.Zone == p1.GraveyardZone, "Dark Cultist died");
            L.Check(wisp.Health == 4, $"the only friendly minion gets +3 Health (health {wisp.Health})");
            L.Check(enemy.Health == 1, "an enemy minion is never picked");

            // The opponent's Cultist buffs the opponent's side.
            var theirs = L.Minion(p2, "FP1_023");
            p1.UsedMana = 0;
            var fireball2 = Entity.FromCard(p1, Cards.FromId("CS2_029"), zone: p1.HandZone);
            L.Check(g.Process(PlayCardTask.SpellTarget(p1, fireball2, theirs)), "Fireball the enemy Dark Cultist");
            L.Check(theirs.Zone == p2.GraveyardZone && enemy.Health == 4 && wisp.Health == 4,
                "the enemy Cultist buffs its own side only");

            // With no friendly minion left the Deathrattle does nothing.
            var alone = L.Minion(p1, "FP1_023");
            p1.UsedMana = 0;
            g.Process(PlayCardTask.SpellTarget(p1, Entity.FromCard(p1, Cards.FromId("CS2_029"), zone: p1.HandZone), wisp));
            p1.UsedMana = 0;
            var fireball3 = Entity.FromCard(p1, Cards.FromId("CS2_029"), zone: p1.HandZone);
            L.Check(wisp.Zone == p1.GraveyardZone, "the Wisp is gone");
            L.Check(g.Process(PlayCardTask.SpellTarget(p1, fireball3, alone)), "Fireball the lone Dark Cultist");
            L.Check(alone.Zone == p1.GraveyardZone && p1.BoardZone.Count == 0 && enemy.Health == 4,
                "a lone Cultist's Deathrattle buffs nobody");
        }
        Console.WriteLine("PASS Dark Cultist Deathrattle");
    }
}
