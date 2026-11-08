using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ChillfallenBaron
    {
        internal const int DbfId = 80647;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            var draw = ComplexTask.Create(new CustomTask((game, controller, source, target, stack) => {
                // Both impacts need a typed target before draw SHOW; Baron stays typed in GRAVEYARD.
                if (game.History) game.PowerHistory.Add(new PowerHistoryMetaData {
                    Type = MetaDataType.TARGET, Info = new List<int> { source.Id }
                });
            }), new DrawTask());
            card.Power = new Power { PowerTask = draw, DeathrattleTask = draw };
        }
    }
}
