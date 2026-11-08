using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Collections.Generic;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class QuestingAssistant
    {
        internal const int DbfId = 118177;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(PlayedQuest);
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 }
            });
            card.TargetingAvailabilityPredicate = (controller, _) => QuestRules.HasPlayedQuest(controller);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (PlayedQuest((IPlayable)s) && t != null)
                    new EffectContext(g, c.Id, s.Id, t.Id, stack).Damage(t.Id, 3);
            }) };
        }

        private static bool PlayedQuest(IPlayable source) => QuestRules.HasPlayedQuest(source.Controller);
    }
}
