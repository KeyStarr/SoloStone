using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
    /// <summary>Resolve a split effect inside its activation, then drain hit reactions.</summary>
    public static class SplitHits
    {
        public static ISimpleTask Inline(int amount, ISimpleTask hit, bool deathProcessing, bool spellDmg = false) =>
            new CustomTask((game, controller, source, target, stack) =>
                Inline(game, controller, source, spellDmg ? amount + SabberStoneCore.Actions.SpellDamageRules.Bonus(source as SabberStoneCore.Model.Entities.IPlayable) : amount,
                    hit, deathProcessing, target));

        public static void Inline(Game game, Controller controller, IEntity source, int amount, ISimpleTask hit,
            bool deathProcessing, IPlayable target = null)
        {
            // Work queued by the hit recipe stays after the burst; damage listeners keep their own nested events.
            game.TaskQueue.StartEvent();
            try
            {
                for (int i = 0; i < amount; i++)
                {
                    hit.Process(game, controller, source, target, new TaskStack());
                    if (deathProcessing) game.DeathProcessingAndAuraUpdate();
                }
                game.ProcessTasks();
            }
            finally { game.TaskQueue.EndEvent(); }
        }
    }
}
