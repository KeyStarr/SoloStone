using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TheBlackBlood
    {
        internal const int DbfId = 122755;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.Declare(card, "CATA_300t1", "CATA_300t2", "CATA_300t3");
            card.Power = new Power { Trigger = new Trigger(TriggerType.HEAL) {
                TriggerSource = TriggerSource.FRIENDLY_EVENT_SOURCE,
                Condition = new SelfCondition(p => p.Game.CurrentEventData?.EventNumber > 0),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    if (!(s is Minion body) || body.Zone != c.BoardZone || body.IsDead) return;
                    var selected = new TaskStack { Playables = c.Opponent.BoardZone.GetAll().Where(m => !m.IsDead).Cast<IPlayable>().ToArray() };
                    if (new RandomTask(1, EntityType.STACK).Process(g, c, s, t, selected) != TaskState.COMPLETE) return;
                    new AttackTask(EntityType.SOURCE, EntityType.STACK).Process(g, c, s, t, selected);
                })
            } };
        }
        internal static void ConfigureBody(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var damaged = c.BoardZone.GetAll().Cast<ICharacter>().Append(c.Hero)
                        .Where(m => m.Damage > 0 && !m.ToBeDestroyed).ToArray();
                    if (damaged.Length == 0) return;
                    damaged[g.Random.Next(damaged.Length)].TakeHeal((IPlayable)s, 3);
                })
            } };
        }
    }
}
