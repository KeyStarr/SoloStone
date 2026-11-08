using SabberStoneCore.Actions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.Model
{
    /// <summary>Hand membership immediately after a committed add/remove, before hand trigger callbacks.</summary>
    public readonly struct HandCountFact
    {
        public int Before { get; }
        public int After { get; }
        public int Maximum { get; }
        internal HandCountFact(int before, int after, int maximum)
        { Before = before; After = after; Maximum = maximum; }
    }

    internal sealed class HandCountQuestTask : SimpleTask
    {
        private readonly HandCountFact _fact;
        internal HandCountQuestTask(HandCountFact fact) { _fact = fact; IsTrigger = true; }
        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null)
        {
            // Qualification and stage ordering were latched at the committed transition.
            if (_fact.Before != _fact.After) QuestRules.Advance(source as Spell);
            return TaskState.COMPLETE;
        }
    }
}
