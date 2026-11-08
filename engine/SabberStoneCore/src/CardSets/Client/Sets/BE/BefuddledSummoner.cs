using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BefuddledSummoner
    {
        internal const int DbfId = 130092;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (c.BoardZone.IsFull) return;
                    var enemies = c.Opponent.BoardZone.Where(m => !m.IsDead && !m.Untouchable).ToArray();
                    if (enemies.Length == 0) return;
                    var original = enemies[g.Random.Next(enemies.Length)]; g.OnRandomHappened(true);
                    var copy = (Minion)Generic.Copy(c, s, original, Zone.PLAY, preserveBoardMinionState: true);
                    g.DeathProcessingAndAuraUpdate();
                    if (copy.IsDead || copy.Zone != c.BoardZone || copy.Untouchable || original.IsDead
                        || original.Zone != c.Opponent.BoardZone || original.Untouchable) return;
                    new AttackTask(EntityType.STACK, EntityType.TARGET).Process(g, c, s, original,
                        new TaskStack { Playables = new[] { copy } });
                })
            } };
        }
    }
}
