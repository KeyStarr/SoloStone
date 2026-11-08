using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Siamat
    {
        internal const int DbfId = 111464;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CORE_ULD_178/v1",
                new DiscoverThenContinueTask("ULD_178a", "ULD_178a2", "ULD_178a3", "ULD_178a4"),
                new CustomTask((g, c, s, t, stack) => {
                    Grant(g, c, s, t, stack);
                    stack.Number = stack.Playables.Single().Card.AssetId;
                    stack.Playables.Clear();
                }),
                SecondChoice(54149, "ULD_178a2", "ULD_178a3", "ULD_178a4"),
                SecondChoice(54150, "ULD_178a", "ULD_178a3", "ULD_178a4"),
                SecondChoice(54151, "ULD_178a", "ULD_178a2", "ULD_178a4"),
                SecondChoice(54152, "ULD_178a", "ULD_178a2", "ULD_178a3")) };
        }

        // Fixed registered branches retain the first pick in the serializable task stack.
        private static SuspendableTask SecondChoice(int first, params string[] remaining) =>
            StateTaskList.Suspendable("client/CORE_ULD_178/second/" + first,
                new NumberConditionTask(first, RelaSign.EQ),
                new FlagTask(false, EffectContext.Task(ctx => TaskState.STOP)),
                new DiscoverThenContinueTask(remaining[0], remaining.Skip(1).ToArray()),
                new CustomTask(Grant));

        private static void Grant(Game game, Controller controller, IEntity source, IPlayable target, TaskStack stack)
        {
            if (!(source is Minion minion) || minion.Zone != controller.BoardZone) return;
            var tag = stack.Playables.Single().Card.Id switch {
                "ULD_178a" => GameTag.WINDFURY,
                "ULD_178a2" => GameTag.DIVINE_SHIELD,
                "ULD_178a3" => GameTag.TAUNT,
                "ULD_178a4" => GameTag.RUSH,
                _ => throw new System.InvalidOperationException("Unknown Siamat option.")
            };
            // Publish before the legacy effect writes NativeTags and applies attack readiness.
            minion[tag] = 1;
            new Effect(tag, EffectOperator.SET, 1).ApplyTo(minion);
        }

        internal static void ConfigureOption(Card card) { card.Implemented = true; }
    }
}
