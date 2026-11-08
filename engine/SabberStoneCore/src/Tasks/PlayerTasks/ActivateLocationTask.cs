using SabberStoneCore.Actions;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.PlayerTasks
{
    public sealed class ActivateLocationTask : PlayerTask
    {
        internal long DecisionId { get; }
        public ActivateLocationTask(Controller controller, IPlayable source, IPlayable target = null)
        {
            PlayerTaskType = PlayerTaskType.ACTIVATE_LOCATION;
            Game = controller.Game;
            Controller = controller;
            Source = source;
            ActionTarget = target;
            DecisionId = Game.OptionsDecisionId;
        }
        public override bool Process() => LocationRules.Activate(Controller, Source, ActionTarget);
        public override string FullPrint() => $"ActivateLocationTask => [{Controller.Name}] {Source} => {ActionTarget}";
    }
}
