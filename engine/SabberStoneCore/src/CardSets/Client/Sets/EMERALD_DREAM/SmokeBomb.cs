using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.CardSets.Client;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SmokeBomb
    {
        internal const int DbfId = 115647;
        private static OfflinePoolFilter Filter() => OfflinePoolFilter.Union(
            OfflinePoolFilter.Discover(type: CardType.MINION, keyword: GameTag.COMBO),
            OfflinePoolFilter.Discover(type: CardType.MINION, keyword: GameTag.BATTLECRY),
            OfflinePoolFilter.Discover(type: CardType.MINION, keyword: GameTag.STEALTH));
        internal static PoolRequirement[] DeclarePools() => new[] { new PoolRequirement("keyword-union", Filter(), new[] { "ROGUE" }) };
        [StandardCard("FIR_920", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/FIR_920/v1",
                new DiscoverWithDarkGiftThenContinueTask(Filter(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null) return;
                    Generic.RemoveFromZone(c, chosen);
                    Generic.AddHandPhase(c, chosen);
                    DarkGifts.ResolveSweetDreams(chosen);
                })) };
        }
    }
}
