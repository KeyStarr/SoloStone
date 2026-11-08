using SabberStoneCore.Model.Effects;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks
{
    /// <summary>Explicit opt-in to resumable execution. Register once at module initialization.</summary>
    public sealed class SuspendableTask : SimpleTask
    {
        public string RecipeKey { get; }
        private SuspendableTask(string key) { EffectFrameRecipes.Resolve(key); RecipeKey = key; }
        internal static SuspendableTask FromDefinitionKey(string key) => new SuspendableTask(key);
        internal SuspendableTask(string key, ISimpleTask[] steps)
        {
            EffectFrameRecipes.Register(key, steps);
            RecipeKey = key;
        }

        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null)
        {
            bool automatic = controller[Actions.Generic.AutomaticDecisionTag] != 0;
            if (!automatic && game.SpellCompletion?.Stage != DecisionCompletionStage.Recipe)
                return game.Resolution.RunEffect(game, RecipeKey, controller, source, target, stack ?? new TaskStack());
            // Selector settlement may drain tasks; isolate the queued caller so it cannot execute again.
            game.TaskQueue.StartEvent();
            try
            {
                if (!automatic)
                    return game.Resolution.RunEffect(game, RecipeKey, controller, source, target, stack ?? new TaskStack());
                // Indirect casts own no player completion; finish their choices before returning to the caller.
                game.Resolution.RunAutomaticEffect(game, RecipeKey, controller, source, target, stack ?? new TaskStack());
                game.ProcessTasks();
                return TaskState.COMPLETE;
            }
            finally { game.TaskQueue.EndEvent(); }
        }
    }
}
