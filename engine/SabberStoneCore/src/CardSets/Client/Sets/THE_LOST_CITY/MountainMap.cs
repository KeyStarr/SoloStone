using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MountainMap
    {
        internal static OfflinePoolFilter Pool() => OfflinePoolFilter.Discover(type: CardType.MINION);
        [StandardCard("TLC_464", 118225)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("b16/mountain-map/v1",
                new DiscoverThenContinueTask(Offers, DiscoverRealRules.PrepareDiscover)) };
        }
        private static Card[] Offers(Game g, Controller c, IEntity source, TaskStack stack)
        {
            var used = g.HistoryFacts.Query.Plays().Where(p => p.ControllerId == c.Id && p.CardType == CardType.MINION)
                .SelectMany(p => p.Races).ToHashSet();
            if (used.Contains(Race.ALL)) return Array.Empty<Card>();
            var remaining = SpiritOfTheMountain.Types.Where(r => !used.Contains(r)).ToArray();
            var candidates = OfflineCardPools.Candidates(g, c.HeroClass, Pool(), source.Card.AssetId, sourceClassMask: OfflineCardPools.PrintedClassMask(source.Card))
                .Where(card => remaining.Any(card.IsRace)).ToList();
            return DiscoverRealRules.Take(candidates, 3, g).ToArray();
        }
        [StandardCard("TLC_464e", 118226)]
        internal static void ConfigureWatch(Card card) => CultistMap.ConfigureMapWatch(card);
    }
}
namespace SabberStoneCore.CardSets.Client
{
    internal static class B16MountainPool
    {
        [OfflinePool("TLC_464", 118225)]
        internal static PoolRequirement[] Declare() => new[] { new PoolRequirement("minion", Sets.MountainMap.Pool(), new[] { "SHAMAN" }) };
    }
}
