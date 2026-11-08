using System.Linq;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DefiledSpear
    {
        internal const int DbfId = 114844;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.HERO,
                Condition = new SelfCondition(attacker => attacker.Game.CurrentEventData?.EventTarget?.Controller == attacker.Controller.Opponent),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    int attacked = g.CurrentEventData.EventTarget.Id;
                    var enemies = c.Opponent.BoardZone.GetAll().Cast<IPlayable>().Append(c.Opponent.Hero)
                        .Where(enemy => enemy.Id != attacked).ToArray();
                    if (enemies.Length == 0) return;
                    var other = enemies[g.Random.Next(enemies.Length)];
                    g.OnRandomHappened(true);
                    new DamageTask(c.Hero.AttackDamage, EntityType.TARGET).Process(g, c, s, other, stack);
                })
            } };
        }
    }
}
