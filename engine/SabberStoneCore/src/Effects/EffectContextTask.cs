using System;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

// Keep task recipes in the legacy adapter namespace so the snapshot registry discovers them.
namespace SabberStoneCore.Tasks.SimpleTasks
{
    internal sealed class EffectContextTask : SimpleTask
    {
        private readonly Func<EffectContext, TaskState> _resolve;
        internal EffectContextTask(Func<EffectContext, TaskState> resolve) =>
            _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null) =>
            _resolve(new EffectContext(game, controller.Id, source.Id, target?.Id, stack));
    }
}
