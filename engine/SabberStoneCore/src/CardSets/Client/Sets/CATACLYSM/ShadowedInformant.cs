using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ShadowedInformant
    {
        internal const int DbfId = 123385;
        internal static CardClass[] Classes => PoolRequirement.PlayableClasses.Select(PoolRequirement.HeroClass).ToArray();
        internal static OfflinePoolFilter Filter(CardClass cls) => new OfflinePoolFilter(classMode: PoolClassMode.Exact,
            exactClass: cls, type: CardType.SPELL);
        [OfflinePool("CATA_614", DbfId)]
        internal static PoolRequirement[] Pools() => Classes.Select(cls => new PoolRequirement(
            "selected-class-" + cls, Filter(cls), new[] { "NEUTRAL" })).ToArray();

        [StandardCard("CATA_614", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                HandEntryTask = new CustomTask((g, c, s, t, stack) => {
                    if (s[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0) Reroll(g, s);
                }),
                Trigger = new Trigger(TriggerType.TURN_START) { TriggerActivation = TriggerActivation.HAND,
                    SingleTask = new CustomTask((g, c, s, t, stack) => {
                        if (s.Zone == c.HandZone) Reroll(g, s);
                    }) },
                PowerTask = new DecisionBattlecryTask("client/CATA_614/v1",
                    new DiscoverThenContinueTask((g, c, s, stack) => OfflineCardPools.SampleDistinct(g,
                        Filter((CardClass)s[GameTag.TAG_SCRIPT_DATA_NUM_1]), c.HeroClass, DbfId), DiscoverRealRules.PrepareDiscover),
                    new AddStackTo(EntityType.HAND))
            };
        }
        private static void Reroll(Game game, SabberStoneCore.Model.Entities.IEntity card)
        {
            var candidates = Classes.Where(cls => (int)cls != card[GameTag.TAG_SCRIPT_DATA_NUM_1]).ToArray();
            card[GameTag.TAG_SCRIPT_DATA_NUM_1] = (int)candidates[game.Random.Next(candidates.Length)];
        }
    }
}
