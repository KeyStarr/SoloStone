using System;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
    /// <summary>A declared offline pool Discover with saved gift pairings and a STACK continuation.</summary>
    public sealed class DiscoverWithDarkGiftThenContinueTask : SimpleTask
    {
        private readonly OfflinePoolFilter _filter;
        private readonly PoolCostBinding _costBinding;
        private readonly int? _recipeDbfId;
        public DiscoverWithDarkGiftThenContinueTask(OfflinePoolFilter filter, PoolCostBinding costBinding = PoolCostBinding.None,
            int? recipeDbfId = null)
        {
            if (!Enum.IsDefined(typeof(PoolCostBinding), costBinding)) throw new ArgumentOutOfRangeException(nameof(costBinding));
            _filter = filter; _costBinding = costBinding; _recipeDbfId = recipeDbfId;
        }

        internal TaskState Open(Game game, Controller controller, IEntity source, IPlayable target, TaskStack stack)
        {
            if (game.Player1.Choice != null || game.Player2.Choice != null)
                throw new InvalidOperationException("Cannot suspend an effect over an existing choice.");
            new EffectContext(game, controller.Id, source.Id, target?.Id, stack)
                .DiscoverWithDarkGift(_filter, ChoiceAction.STACK, costBinding: _costBinding, recipeDbfId: _recipeDbfId);
            return controller.Choice != null ? TaskState.SUSPENDED : TaskState.COMPLETE;
        }

        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null) =>
            throw new NotSupportedException("DiscoverWithDarkGiftThenContinueTask requires a registered suspendable recipe.");
    }
}
