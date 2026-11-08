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
    internal static class TheSkeletonKey
    {
        internal static OfflinePoolFilter Pool() => OfflinePoolFilter.Discover(type: CardType.SPELL);
        [StandardCard("JAIL_319", 126116)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("b16/skeleton-key/v1",
                new DiscoverThenContinueTask(Offers, DiscoverRealRules.PrepareDiscover),
                new CustomTask((g,c,s,t,stack) => {
                    var selected = stack.Playables.LastOrDefault();
                    if (g.State != State.COMPLETE && selected != null && selected.Card.Id != "JAIL_319t")
                        Generic.AddHandPhase(c, c.SetasideZone.Remove(selected));
                })) };
        }
        private static Card[] Offers(Game g, Controller c, IEntity source, TaskStack stack)
        {
            if (g.State == State.COMPLETE) return Array.Empty<Card>();
            var cards = OfflineCardPools.SampleDistinct(g, Pool(), c.HeroClass, source.Card.AssetId, sourceClassMask: OfflineCardPools.PrintedClassMask(source.Card));
            return cards.Length == 0 ? cards : cards.Append(Cards.FromId("JAIL_319t")).ToArray();
        }
        [StandardCard("JAIL_319t", 131199)]
        internal static void ConfigureRefresh(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; }
    }
}
namespace SabberStoneCore.CardSets.Client
{
    internal static class B16SkeletonPool
    {
        [OfflinePool("JAIL_319", 126116)]
        internal static PoolRequirement[] Declare() => new[] { new PoolRequirement("spell", Sets.TheSkeletonKey.Pool(), new[] { "MAGE" }) };
    }
}
