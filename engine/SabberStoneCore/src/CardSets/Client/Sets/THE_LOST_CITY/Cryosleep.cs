using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Cryosleep
    {
        internal const int DbfId = 117622;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                bool kindred = KindredRules.Match((IPlayable)s);
                new DamageTask(4, EntityType.TARGET, true).Process(g, c, s, t, stack);
                new DrawTask(1).Process(g, c, s, t, stack);
                int repeats = kindred ? KindredRules.BeginMatchedClause((IPlayable)s) : 0;
                for (int i = 0; i < repeats; i++) new DrawTask(1).Process(g, c, s, t, stack);
            }) };
        }
    }
}
