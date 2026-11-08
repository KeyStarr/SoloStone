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
    internal static class FlamesOfInfinity
    {
        [StandardCard("END_024", 121187)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                TriggerSource = TriggerSource.ALL, EitherTurn = true,
                SingleTask = ComplexTask.Create(
                    new ConditionTask(EntityType.SOURCE, SelfCondition.IsOpTurn,
                        new SelfCondition(me => me.Controller.Opponent.BoardZone.Any(m => !m.Untouchable))),
                    new FlagTask(true, ComplexTask.Secret(new CustomTask((g,c,s,t,stack) => {
                        var candidates = c.Opponent.BoardZone.Where(m => !m.Untouchable).ToArray();
                        if (candidates.Length == 0) return;
                        int health = candidates.Max(m => m.Health);
                        var tied = candidates.Where(m => m.Health == health).ToArray();
                        Generic.DamageCharFunc.Invoke((IPlayable)s, tied[g.Random.Next(tied.Length)], int.MaxValue, true);
                    }))))
            } };
        }
    }
}
