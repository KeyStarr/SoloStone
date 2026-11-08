using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StaffOfTrickery
    {
        internal const int DbfId = 126243;
        internal static OfflinePoolFilter Filter() => new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.DRUID);
        internal static PoolRequirement[] DeclarePools() => new[] { new PoolRequirement("druid-card", Filter(), new[] { "DRUID" }) };
        [StandardCard("JAIL_875", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.HERO,
                SingleTask = AfterAttackRules.Register(DbfId,
                    new DiscoverThenContinueTask(Filter(), recipeDbfId: DbfId),
                    new CustomTask((g, c, s, t, stack) => {
                        var chosen = stack.Playables.LastOrDefault();
                        if (chosen == null) return;
                        using (c.HandZone.DeferCardStateEntry())
                        {
                            Generic.AddHandPhase(c, c.SetasideZone.Remove(chosen));
                            if (chosen.Zone == c.HandZone)
                                Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_875e"), (IPlayable)s, chosen, stack.Number);
                        }
                    }))
            } };
        }
        [StandardCard("JAIL_875e", 126244)]
        internal static void ConfigureDiscount(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Cost.Effect(EffectOperator.SUB, 0)) { UseScriptTag = true } };
        }
    }
}

namespace SabberStoneCore.CardSets.Client
{
    internal static class B17StaffOfTrickeryPools
    {
        [OfflinePool("JAIL_875", 126243)]
        internal static PoolRequirement[] DeclarePools() => Sets.StaffOfTrickery.DeclarePools();
    }
}
