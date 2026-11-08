using SabberStoneCore.Actions;
using SabberStoneCore.Model.Entities;
namespace SabberStoneCore.Tasks.PlayerTasks
{
    public sealed class ActivateOldGodTask : PlayerTask
    {
        internal long DecisionId { get; }
        public ActivateOldGodTask(Controller owner, Minion source)
        {
            PlayerTaskType = PlayerTaskType.ACTIVATE_OLD_GOD;
            Game = owner.Game; Controller = owner; Source = source; DecisionId = Game.OptionsDecisionId;
        }
        public override bool Process() => Source is Minion body && OldGodRules.Activate(Controller,body);
        public override string FullPrint() => $"ActivateOldGodTask => [{Controller.Name}] {Source}";
    }
}
