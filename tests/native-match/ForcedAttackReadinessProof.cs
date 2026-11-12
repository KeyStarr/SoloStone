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
    private static void ForcedAttackReadinessIsPublished()
    {
        foreach (bool windfury in new[] { false, true })
        foreach (int used in new[] { 0, 1 })
        {
            var capture = new Capture();
            var match = StartFixture(capture, "CS2_231", null); var g = match.Game; var p = g.Player1;
            Minion Summon(Controller owner, string id)
            {
                var m = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(owner, Cards.FromId(id));
                Generic.SummonBlock(g, m, -1, null); return m;
            }
            var attacker = Summon(p, "CS2_171"); attacker.BaseHealth = 30; attacker.HasWindfury = windfury;
            var target = Summon(p.Opponent, "CS2_182"); target.BaseHealth = 40;
            FlushHistory(match);
            if (used != 0) { InvokeProcess(match, MinionAttackTask.Any(p, attacker, target)); FlushHistory(match); }
            var spell = Generic.DrawCard(p, Cards.FromId("TRL_119"));
            p.BaseMana = 10; p.UsedMana = 0;
            FlushHistory(match);
            var before = ReplayNativeEntityTags(NativeHistory(capture), attacker.Id);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, PlayCardTask.Any(p, spell, attacker)); FlushHistory(match);
            bool exhausted = used == 1 && !windfury;
            var shown = ReplayNativeEntityTags(NativeHistory(capture), attacker.Id);
            Check(attacker.NumAttacksThisTurn == used && attacker.IsExhausted == exhausted
                && shown.GetValueOrDefault((int)GameTag.NUM_ATTACKS_THIS_TURN) == before.GetValueOrDefault((int)GameTag.NUM_ATTACKS_THIS_TURN)
                && shown.GetValueOrDefault((int)GameTag.EXHAUSTED) == (exhausted ? 1 : 0),
                $"forced combat publishes unchanged readiness: windfury={windfury} used={used} engine={attacker.NumAttacksThisTurn}/{attacker.IsExhausted} wire={shown.GetValueOrDefault((int)GameTag.NUM_ATTACKS_THIS_TURN)}/{shown.GetValueOrDefault((int)GameTag.EXHAUSTED)}");
            Check(!History(capture, cursor).Any(entry => entry.HasTagChange && entry.TagChange.Entity == attacker.Id
                && (entry.TagChange.Tag == (int)GameTag.EXHAUSTED || entry.TagChange.Tag == (int)GameTag.NUM_ATTACKS_THIS_TURN)),
                "forced combat sends no transient false exhaustion or spent-attack tags");
            Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == attacker.Id)
                == !exhausted, "native projected readiness agrees with legal attack options");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS forced combat native readiness: unused, Windfury, spent; unchanged counter/exhaustion; no transient tags");
    }
}
