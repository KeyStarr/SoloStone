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
    internal static class TimelooperToki
    {
        private const GameTag First = (GameTag)32410;
        internal static OfflinePoolFilter Pool() => OfflinePoolFilter.Random(type: CardType.SPELL, scope: PoolScope.Past);
        [StandardCard("TIME_861", 119741)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                var tracker = Enchantment.GetInstance(c, (IPlayable)s, c, Cards.FromId("TIME_861e1"));
                for (int i = 0; i < 3; i++) {
                    var spell = OfflineCardPools.Pick(g, Pool(), c.HeroClass, s.Card.AssetId);
                    tracker[(GameTag)((int)First + i)] = spell == null ? -1 : Generic.DrawCard(c, spell).Id;
                }
            }) };
        }
        internal static void Played(Controller c, IPlayable played, CardPlayedFact fact)
        {
            if (fact.CardType != CardType.SPELL) return;
            foreach (var tracker in c.AppliedEnchantments?.Where(e => e.Card.Id == "TIME_861e1").ToArray() ?? Array.Empty<Enchantment>()) {
                for (int i = 0; i < 3; i++) {
                    var tag = (GameTag)((int)First + i);
                    if (tracker[tag] == played.Id) tracker[tag] = 0;
                }
                if (Enumerable.Range(0,3).All(i => tracker[(GameTag)((int)First + i)] == 0)) {
                    tracker.Remove(); Generic.DrawCard(c, Cards.FromId("TIME_861"));
                }
            }
        }
        [StandardCard("TIME_861e1", 122364)]
        internal static void ConfigureTracker(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power(); }
    }
}
namespace SabberStoneCore.CardSets.Client
{
    internal static class B16TokiPool
    {
        [OfflinePool("TIME_861", 119741)]
        internal static PoolRequirement[] Declare() => new[] { new PoolRequirement("past-spell", Sets.TimelooperToki.Pool(), new[] { "MAGE" }) };
    }
}
