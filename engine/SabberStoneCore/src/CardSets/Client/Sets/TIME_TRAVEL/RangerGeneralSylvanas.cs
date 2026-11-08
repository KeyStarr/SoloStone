using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RangerGeneralSylvanas
    {
        internal const int DbfId = 119707;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.PoweredUpWhen(PlayedPartner);
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                int repeats = PlayedPartner(ctx.History, ctx.ControllerId)
                    ? Repetitions(ctx.History, ctx.ControllerId, RangerCaptainAlleria.DbfId, RangerInitiateVereesa.DbfId) : 1;
                for (int i = 0; i < repeats; i++)
                    ctx.RunLegacy(new DamageTask(2, EntityType.ENEMIES));
                return TaskState.COMPLETE;
            }) };
        }

        // Count distinct named partners played by this controller; additional copies do not add repeats.
        internal static int Repetitions(HistoryQuery history, int controllerId, int first, int second) => 1 + history.Plays()
            .Where(f => f.ControllerId == controllerId && f.DefinitionKnown && (f.DbfId == first || f.DbfId == second))
            .Select(f => f.DbfId).Distinct().Count();
        private static bool PlayedPartner(IPlayable source) => PlayedPartner(source.Game.HistoryFacts.Query, source.Controller.Id);
        private static bool PlayedPartner(HistoryQuery history, int controllerId) =>
            Repetitions(history, controllerId, RangerCaptainAlleria.DbfId, RangerInitiateVereesa.DbfId) > 1;
    }
}
