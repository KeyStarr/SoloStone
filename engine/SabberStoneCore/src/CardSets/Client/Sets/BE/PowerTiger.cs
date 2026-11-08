using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PowerTiger
    {
        internal const int DbfId = 131418;
        [StandardCard("BE_999", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                EitherTurn = true,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (!(s is Minion source) || source.IsDead || source.Zone != c.BoardZone || source.Untouchable) return;
                    var enemies = c.Opponent.BoardZone.Where(m => !m.IsDead && !m.Untouchable)
                        .Cast<IPlayable>().Append(c.Opponent.Hero).ToArray();
                    var enemy = enemies[g.Random.Next(enemies.Length)];
                    g.OnRandomHappened(true);
                    new AttackTask(EntityType.SOURCE, EntityType.TARGET).Process(g, c, s, enemy, stack);
                })
            } };
        }
    }
}
