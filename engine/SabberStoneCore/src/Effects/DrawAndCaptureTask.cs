using System;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
    /// <summary>Captures only this instruction's physical draws, in draw order.</summary>
    public sealed class DrawAndCaptureTask : SimpleTask
    {
        private readonly int _count;
        public DrawAndCaptureTask(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            _count = count;
        }
        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null)
        {
            var frame = game.Resolution.CurrentEffect ?? throw new InvalidOperationException("Capture requires an effect frame.");
            var captured = new List<int>();
            for (int i = 0; i < _count && game.State == SabberStoneCore.Enums.State.RUNNING && !controller.Hero.IsDead && !controller.Opponent.Hero.IsDead; i++)
            {
                var drawn = Generic.Draw(controller);
                if (drawn != null && drawn.Controller == controller && drawn.Zone == controller.HandZone) captured.Add(drawn.Id);
            }
            frame.CapturedCandidateIds = captured.ToArray();
            return game.State != SabberStoneCore.Enums.State.RUNNING || controller.Hero.IsDead || controller.Opponent.Hero.IsDead
                ? TaskState.STOP : TaskState.COMPLETE;
        }
    }
}
