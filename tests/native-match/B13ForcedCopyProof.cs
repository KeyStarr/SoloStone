using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    private static void BefuddledCopyStateIsPublished()
    {
        var capture = new Capture(); var match = StartFixture(capture, "CS2_231", null); var g = match.Game;
        var source = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(g.Player1, Cards.FromId("BE_196"));
        Generic.SummonBlock(g, source, -1, null);
        var enemy = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(g.Player2, Cards.FromId("CS2_122"));
        Generic.SummonBlock(g, enemy, -1, null); enemy.Silence();
        enemy.BaseHealth = 20; enemy.AttackDamage = 0; enemy.Damage = 2;
        var aura = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(g.Player1, Cards.FromId("CS2_122"));
        Generic.SummonBlock(g, aura, -1, null); g.DeathProcessingAndAuraUpdate(); FlushHistory(match);
        Check(g.Process(EndTurnTask.Any(g.Player1)), "Befuddled end turn accepted"); FlushHistory(match);
        var copy = g.Player1.BoardZone.Single(m => m.Id != source.Id && m.Id != aura.Id);
        var tags = ReplayNativeEntityTags(NativeHistory(capture), copy.Id);
        foreach (var pair in new[] { (GameTag.ATK, 1), (GameTag.HEALTH, 20), (GameTag.DAMAGE, 2),
            (GameTag.SILENCED, 1), (GameTag.EXHAUSTED, 1), (GameTag.NUM_ATTACKS_THIS_TURN, 0) })
            Check(tags.GetValueOrDefault((int)pair.Item1) == pair.Item2, "Unpinned_BefuddledCopyState native " + pair.Item1);
        Check(copy.Controller == g.Player1 && copy.IsSilenced && copy.AttackDamage == 1 && copy.Damage == 2
            && copy.NumAttacksThisTurn == 0 && enemy.Damage == 3,
            "Unpinned_BefuddledCopyState: destination aura, preserved damage/Silence and actual forced combat");
        capture.SerializeAll();
        Console.WriteLine("PASS Unpinned_BefuddledCopyState native packets: damage, Silence, destination aura, forced readiness");
    }
}
