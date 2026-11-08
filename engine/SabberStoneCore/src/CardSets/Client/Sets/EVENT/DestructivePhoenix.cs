using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DestructivePhoenix
    {
        [StandardCard("CATA_EVENT_001", 126700)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("event/phoenix/v1",
                new CustomTask((g,c,s,t,stack) => g.Resolution.CurrentEffect.CapturedCandidateIds = c.HandZone.Where(p => !HandTimelineRules.IsBurning(p)).Select(p => p.Id).ToArray()),
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.CapturedEntities)),
                new CustomTask((g,c,s,t,stack) => {
                    int chosen = g.Resolution.CurrentEffect.LastSelectionEntityId;
                    if (chosen != 0) HandTimelineRules.Ignite((IPlayable)s, g.IdEntityDic[chosen]);
                })) };
        }
        [StandardCard("CATA_EVENT_001e", 130827)]
        internal static void Fire(Card card)
        { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ExpiresOnHandExit = true; card.Power = new Power(); }
    }
}
