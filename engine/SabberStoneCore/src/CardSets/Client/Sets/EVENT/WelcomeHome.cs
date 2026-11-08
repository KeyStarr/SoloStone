using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WelcomeHome
    {
        internal static OfflinePoolFilter Pool() => OfflinePoolFilter.Random(CardType.MINION, 3, 3);
        [StandardCard("TIME_EVENT_997", 123416)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.PlayAvailabilityPredicate = (c, _) => LocationRules.ReopenTargets(c).Length != 0;
            card.Power = new Power { BattlecryLocationTarget = LocationTargetKind.Location,
                PowerTask = new CustomTask((g,c,s,t,stack) => {
                    // Character-only legacy spell dispatch retains a location target on the source entity.
                    var target = t as BoardObject ?? (g.IdEntityDic.TryGetValue(((IPlayable)s).CardTarget, out var e) ? e as BoardObject : null);
                    if (target == null && ((IPlayable)s).CardTarget == 0) {
                        var locations = LocationRules.ReopenTargets(c);
                        if (locations.Length != 0) target = locations[g.Random.Next(locations.Length)];
                    }
                    if (!LocationRules.ValidReopenTarget(c, target)) return;
                    LocationRules.Reopen(target);
                    Generic.AddEnchantmentBlock(g, Cards.FromId("TIME_EVENT_997e"), (IPlayable)s, target);
                    if (s[GameTag.PREMIUM] != 0) target.AppliedEnchantments.Last(e => e.Card.Id == "TIME_EVENT_997e")[GameTag.PREMIUM] = s[GameTag.PREMIUM];
                }) };
        }
        [StandardCard("TIME_EVENT_997e", 123417)]
        internal static void Enchantment(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g,c,s,t,stack) => {
                if (c.BoardZone.IsFull) return;
                var picked = OfflineCardPools.Pick(g, Pool(), c.HeroClass, 123416, PoolDestination.Summon);
                if (picked == null) return;
                var dead = (s as Model.Entities.Enchantment)?.Target as BoardObject ?? s as BoardObject;
                int position = dead?.LastBoardPosition ?? c.BoardZone.SlotCount;
                var minion = (Minion)Entity.FromCard(c, picked, new EntityData {
                    [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id, [GameTag.PREMIUM] = (t as Model.Entities.Enchantment)?[GameTag.PREMIUM] ?? s[GameTag.PREMIUM] });
                Generic.SummonBlock(g,minion,position,s);
            }) };
        }
    }
}
namespace SabberStoneCore.CardSets.Client
{
    internal static class WelcomeHomePools
    {
        [OfflinePool("TIME_EVENT_997", 123416)]
        internal static PoolRequirement[] Declare() => new[] {
            new PoolRequirement("event-welcome-home-three", Sets.WelcomeHome.Pool(), new[] { "NEUTRAL" }, destination: PoolDestination.Summon)
        };
    }
}
