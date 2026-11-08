using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LowSecurityWing
    {
        internal static OfflinePoolFilter Filter() => new OfflinePoolFilter(classMode: PoolClassMode.Exact,
            exactClass: CardClass.SHAMAN, type: CardType.MINION);

        [StandardCard("JAIL_987", 126938)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                Target = LocationTargetKind.None,
                Task = new CustomTask((g, c, s, t, stack) => {
                    var selected = OfflineCardPools.Pick(g, Filter(), c.HeroClass, 126938);
                    if (selected != null) PlayLockRules.LockHand(Generic.DrawCard(c, selected), (IPlayable)s, "JAIL_987e", 0);
                })
            } };
        }

        [StandardCard("JAIL_987e", 129583)]
        internal static void ConfigureLock(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}

namespace SabberStoneCore.CardSets.Client
{
    internal static class B15PlayLockPools
    {
        [OfflinePool("JAIL_987", 126938)]
        internal static PoolRequirement[] DeclarePools() => new[] {
            new PoolRequirement("low-security-shaman-minion", Sets.LowSecurityWing.Filter(), new[] { "SHAMAN" })
        };
    }
}
