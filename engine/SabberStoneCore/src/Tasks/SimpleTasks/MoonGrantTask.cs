using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
    public sealed class MoonGrantTask : SimpleTask
    {
        private readonly int _reduction;
        public MoonGrantTask(int reduction) => _reduction = reduction;

        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null)
        {
            Generic.RemoveFromZone(controller, target);
            CostRules.AddModifier((Playable)target, EffectOperator.SUB, _reduction);
            TemporaryRules.Make(target);
            Generic.AddHandPhase(controller, target);
            return TaskState.COMPLETE;
        }
    }
}
