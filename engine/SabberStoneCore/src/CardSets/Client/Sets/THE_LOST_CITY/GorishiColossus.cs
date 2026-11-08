using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GorishiColossus
    {
        internal const int DbfId = 117872;
        internal const int EffectDbfId = 123168;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                ctx.AddPlayerEffect("TLC_631e");
                return TaskState.COMPLETE;
            }) };
        }

        internal static void ConfigureEffect(Card card)
        {
            card.Implemented = true;
            card.Power = new Power();
            QuestRules.RegisterDamageListener(EffectDbfId, (listener, fact) =>
                fact.Amount == 2 && fact.SourceControllerId == listener.Controller.Id
                    && fact.TargetControllerId != fact.SourceControllerId && fact.SourceDbfId != EffectDbfId
                    ? new GorishiExtraDamageTask(((Enchantment)listener).ScriptTag1) : null);
        }
    }
}

namespace SabberStoneCore.Tasks.SimpleTasks
{
    // Ordinary serializable task state; freeze the stack count at the qualifying hit.
    public sealed class GorishiExtraDamageTask : SimpleTask
    {
        private readonly int _stacks;
        public GorishiExtraDamageTask(int stacks) { _stacks = stacks; IsTrigger = true; }

        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null)
        {
            if (target is Character character)
                for (int i = 0; i < _stacks && !character.IsDead; i++)
                {
                    if (game.History) game.PowerHistory.SelectResolvedTarget(character);
                    // The enchantment source marks these hits so no Colossus stack can recurse.
                    Generic.DamageCharFunc((IPlayable)source, character, 2, false);
                }
            return TaskState.COMPLETE;
        }
    }
}
