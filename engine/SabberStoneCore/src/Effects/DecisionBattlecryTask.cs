using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
    /// <summary>Opt-in battlecry with saved play completion and sequential repetitions.</summary>
    public sealed class DecisionBattlecryTask : SimpleTask
    {
        internal SuspendableTask Recipe { get; }
        internal DecisionBattlecryTask(SuspendableTask recipe) => Recipe = recipe;
        public DecisionBattlecryTask(string key, params ISimpleTask[] steps) =>
            Recipe = StateTaskList.Suspendable(key, steps);

        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null)
        {
            // Replays (including Shudderwock) resolve their compulsory choices synchronously.
            int previous = controller[Actions.Generic.AutomaticDecisionTag];
            controller[Actions.Generic.AutomaticDecisionTag] = 1;
            // Selector reaction drains must not reenter this still-queued Battlecry.
            game.TaskQueue.StartEvent();
            try
            {
                Recipe.Process(game, controller, source, target, stack);
                while (game.Resolution.HasSuspendedEffect)
                    game.Resolution.ResumeEffect(game, controller.Choice.Choices.Choose(game.Random));
                game.ProcessTasks();
                return TaskState.COMPLETE;
            }
            finally { game.TaskQueue.EndEvent(); controller[Actions.Generic.AutomaticDecisionTag] = previous; }
        }
    }
}
