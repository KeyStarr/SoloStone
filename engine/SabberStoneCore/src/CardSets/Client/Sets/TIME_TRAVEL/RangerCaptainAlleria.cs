using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RangerCaptainAlleria
    {
        internal const int DbfId = 119705;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.PoweredUpWhen(PlayedPartner);
            var steps = new ISimpleTask[] {
                Discover(), Keep(),
                Gate(2), Discover(), Keep(),
                Gate(3), Discover(), Keep()
            };
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/TIME_609t1/v1", steps) };
        }

        private static ISimpleTask Gate(int count) => EffectContext.Task(ctx =>
            PlayedPartner(ctx.History, ctx.ControllerId)
                && RangerGeneralSylvanas.Repetitions(ctx.History, ctx.ControllerId, RangerGeneralSylvanas.DbfId, RangerInitiateVereesa.DbfId) >= count
                ? TaskState.COMPLETE : TaskState.STOP);
        private static ISimpleTask Discover() => new DiscoverThenContinueTask(TimeTravelPools.WindrunnerSpell(), recipeDbfId: DbfId);
        private static ISimpleTask Keep() => new CustomTask((g, c, s, t, stack) => {
            var chosen = stack.Playables.SingleOrDefault();
            if (chosen == null) return;
            Generic.RemoveFromZone(c, chosen);
            Generic.AddHandPhase(c, chosen);
        });
        private static bool PlayedPartner(IPlayable source) => PlayedPartner(source.Game.HistoryFacts.Query, source.Controller.Id);
        private static bool PlayedPartner(HistoryQuery history, int controllerId) =>
            RangerGeneralSylvanas.Repetitions(history, controllerId, RangerGeneralSylvanas.DbfId, RangerInitiateVereesa.DbfId) > 1;
    }
}
