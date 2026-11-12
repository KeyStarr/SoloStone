using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class WarptoothSetupTests
{
    public static void TestFissureHeroTradeSummonsChargeFromHand()
    {
        var g = Fixture(); g.Player1.BaseMana = 2; g.Player1.Hero.IsExhausted = false;
        for (int i = 0; i < 3; i++) Board(g, true, "CS2_120");
        var anvil = Board(g, false, "CS2_186"); anvil.AttackDamage = 1;
        var fissure = Entity.FromCard(g.Player1, Cards.FromId("CATA_582"), zone: g.Player1.HandZone);
        var warptooth = (Minion)Entity.FromCard(g.Player1, Cards.FromId("JAIL_421"), zone: g.Player1.HandZone);
        Check(warptooth.Cost > g.Player1.RemainingMana, "combo cannot hard-cast Warptooth");
        g.Process(g.Player1.Options().Single(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source == fissure));
        Check(warptooth.Zone == g.Player1.HandZone, "three damaged characters do not trigger Warptooth");
        g.Process(g.Player1.Options().Single(t => t.PlayerTaskType == PlayerTaskType.HERO_ATTACK && t.ActionTarget == anvil));
        Check(warptooth.Zone == g.Player1.BoardZone && !warptooth.IsExhausted,
            "fourth damaged character summons ready Charge body from hand");
        g.Process(g.Player1.Options().Single(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK
            && t.Source == warptooth && t.ActionTarget == anvil));
        g.Process(EndTurnTask.Any(g.Player1));
        Check(warptooth.Zone == g.Player1.BoardZone && warptooth.Health > 0 && anvil.Zone != g.Player2.BoardZone,
            "combo clears the anvil and retains Warptooth at turn end");
    }
}
