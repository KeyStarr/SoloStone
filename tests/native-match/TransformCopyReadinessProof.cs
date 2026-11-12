using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    private static void TransformCopyReadinessIsPublished()
    {
        foreach (bool alreadyExhausted in new[] { false, true })
        {
            var capture = new Capture();
            var match = StartFixture(capture, "CS2_231", null);
            var game = match.Game;
            game.PowerHistory.Last.Clear();
            var celestia = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("BOT_555"));
            Generic.SummonBlock(game, celestia, -1, null);
            celestia.IsExhausted = alreadyExhausted;
            FlushHistory(match);
            var before = ReplayNativeEntityTags(NativeHistory(capture), celestia.Id);
            Check(before.GetValueOrDefault((int)GameTag.EXHAUSTED) == (alreadyExhausted ? 1 : 0),
                "Celestia fixture publishes initial readiness");

            var sabretooth = Generic.DrawCard(game.Player1, Cards.FromId("TLC_247"));
            FlushHistory(match);
            int cursor = game.PowerHistory.Full.Count;
            InvokeProcess(match, PlayCardTask.Any(game.Player1, sabretooth));
            FlushHistory(match);
            Check(celestia.Card.Id == "TLC_247" && celestia.IsExhausted
                && celestia[GameTag.EXHAUSTED] == 1 && sabretooth.IsExhausted,
                "Celestia becomes exhausted Sabretooth without changing played source readiness");
            var changes = game.PowerHistory.Full.Skip(cursor)
                .OfType<SabberStoneCore.Kettle.PowerHistoryTagChange>()
                .Where(tag => tag.EntityId == celestia.Id && tag.Tag == GameTag.EXHAUSTED).ToArray();
            Check(changes.Length == (alreadyExhausted ? 0 : 1) && changes.All(tag => tag.Value == 1),
                "Celestia transform emits EXHAUSTED=1 exactly when readiness changes");
            var shown = ReplayNativeEntityTags(NativeHistory(capture), celestia.Id);
            Check(shown.GetValueOrDefault((int)GameTag.EXHAUSTED) == 1,
                "Celestia transform exhaustion reaches native client");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Celestia transform-copy readiness: ready/exhausted destination, engine history and native EXHAUSTED=1");
    }
}
