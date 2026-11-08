using System.Collections.Generic;
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
    internal static class WillfulWatcher
    {
        internal const int DbfId = 117916;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                for (int i = 0; i < 3 && c.DeckZone.Count > 0; i++)
                    new MoveToGraveYard(EntityType.TARGET, destroy: true).Process(g, c, s, c.DeckZone.TopCard, stack);
            }) };
        }
    }
}
