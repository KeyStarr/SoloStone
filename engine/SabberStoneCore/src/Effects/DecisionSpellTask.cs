using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
    /// <summary>Hand plays suspend through DecisionCompletion; indirect casts always finish inline.</summary>
    public sealed class DecisionSpellTask : SimpleTask
    {
        internal SuspendableTask Recipe { get; }
        public DecisionSpellTask(string key, params ISimpleTask[] steps) =>
            Recipe = StateTaskList.Suspendable(key, steps);

        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null)
        {
            if (Actions.Generic.IsTrackedSpellBody(game, source))
                return Recipe.Process(game, controller, source, target, stack);
            int previous = controller[Actions.Generic.AutomaticDecisionTag];
            controller[Actions.Generic.AutomaticDecisionTag] = 1;
            game.TaskQueue.StartEvent();
            try
            {
                game.Resolution.RunAutomaticEffect(game, Recipe.RecipeKey, controller, source, target, stack ?? new TaskStack());
                game.ProcessTasks();
                return TaskState.COMPLETE;
            }
            finally { game.TaskQueue.EndEvent(); controller[Actions.Generic.AutomaticDecisionTag] = previous; }
        }
    }
}
