using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Enums;

namespace SabberStoneCore.Tasks.SimpleTasks
{
    /// <summary>A fixed roster slot, preserving Call of the Wild's Huffer/Leokk/Misha order.</summary>
    public sealed class SummonCompanionTask : SimpleTask
    {
        private readonly int _slot;
        public SummonCompanionTask(int slot) { _slot = slot; }
        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null) =>
            new SummonTask(CompanionRules.Roster(controller)[_slot], SummonSide.SPELL)
                .Process(game, controller, source, target, stack);
    }
}
