using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SpectralSight
    {
        internal const int DbfId = 69684;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(new CustomTask((game, controller, source, target, stack) => {
                // The impact's CARDTYPE branch cannot finish on an unrevealed deck card.
                if (game.History) game.PowerHistory.Add(new PowerHistoryMetaData {
                    Type = MetaDataType.TARGET, Info = new List<int> { source.Id }
                });
            }), EffectContext.Task(ctx => {
                ctx.RunLegacy(new DrawTask());
                if (ctx.PlayedFromEdge) ctx.RunLegacy(new DrawTask());
                return TaskState.COMPLETE;
            })) };
        }
    }
}
