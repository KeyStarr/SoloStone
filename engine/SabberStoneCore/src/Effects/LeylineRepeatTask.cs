using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.Tasks.SimpleTasks
{
    /// <summary>One cast, snapshotted magnitude/repetitions, fresh recipients and Spell Damage per instance.</summary>
    public sealed class LeylineRepeatTask : SimpleTask
    {
        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null)
        {
            var spell = (IPlayable)source;
            var snapshot = new TaskStack { Number = LeylineRules.Magnitude(spell), Number1 = LeylineRules.Times(spell) };
            var ctx = new EffectContext(game, controller.Id, source.Id, null, snapshot);
            // Lean: leyline-resolution-snapshot.yaml. State is local to this atomic resolution, never on a recipe.
            for (int i = 0; i < snapshot.Number1; i++)
            {
                game.TaskQueue.StartEvent();
                try
                {
                    switch (source.Card.Id)
                    {
                        case "MEND_500": Burst(game, controller, spell, snapshot.Number); break;
                        case "MEND_502":
                            if (!controller.BoardZone.IsFull)
                            {
                                var card = ctx.PickRandomCard(LeylinePools.Minion(), PoolCostBinding.StackNumber, 123504);
                                if (card != null) ctx.Summon(card.Id, controller.BoardZone.SlotCount);
                            }
                            break;
                        case "MEND_504":
                            var drawn = ctx.DrawEntity();
                            if (drawn != null && drawn.Zone == controller.HandZone)
                                Generic.AddEnchantmentBlock(game, Cards.FromId("MEND_504e"), spell, drawn, snapshot.Number);
                            break;
                        default: throw new InvalidOperationException("Not a printed Leyline: " + source.Card.Id);
                    }
                    game.ProcessTasks();
                }
                // Finish reactions and deaths at the repeat boundary before retargeting.
                finally { game.TaskQueue.EndEvent(); }
                game.DeathProcessingAndAuraUpdate();
            }
            return TaskState.COMPLETE;
        }

        private static void Burst(Game game, Controller controller, IPlayable spell, int amount)
        {
            var targets = controller.Opponent.BoardZone.GetAll().Where(m => m.Health > 0 && !m.ToBeDestroyed).ToArray();
            if (targets.Length == 0) return;
            var minion = targets[game.Random.Next(targets.Length)];
            game.OnRandomHappened(true);
            int health = minion.Health;
            int dealt = Generic.DamageCharFunc(spell, minion, amount, true);
            int excess = Math.Max(0, dealt - health);
            if (excess > 0) Generic.DamageCharFunc(spell, controller.Opponent.Hero, excess, false);
        }
    }
}
