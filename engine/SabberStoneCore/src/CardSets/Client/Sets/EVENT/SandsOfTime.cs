using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SandsOfTime
    {
        internal static readonly CardClass[] Classes = { CardClass.DEATHKNIGHT, (CardClass)14, CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE,
            CardClass.PALADIN, CardClass.PRIEST, CardClass.ROGUE, CardClass.SHAMAN, CardClass.WARLOCK, CardClass.WARRIOR };
        internal static OfflinePoolFilter ClassSpells(CardClass hero) => new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: hero, type: CardType.SPELL);
        internal static OfflinePoolFilter AnyClassSpells() => OfflinePoolFilter.Union(Classes.Select(ClassSpells).ToArray());
        [StandardCard("TIME_EVENT_999", 121675)]
        internal static void Configure(Card card) => ConfigureSpell(card);
        [StandardCard("TIME_EVENT_999t", 122549)]
        internal static void Rewound(Card card) => ConfigureSpell(card);
        private static void ConfigureSpell(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = StateTaskList.Suspendable("event/" + card.Id + "/v1",
                new DiscoverThenContinueTask((g,c,s,stack) => OfflineCardPools.SampleDistinct(g,
                    s.Card.Id == "TIME_EVENT_999t" ? ClassSpells(c.HeroClass) : AnyClassSpells(), c.HeroClass, s.Card.AssetId), DiscoverRealRules.PrepareDiscover),
                new CustomTask((g,c,s,t,stack) => {
                    var chosen = stack.Playables.LastOrDefault();
                    if (chosen != null) Generic.AddHandPhase(c, c.SetasideZone.Remove(chosen));
                })) };
        }
        [OfflinePool("TIME_EVENT_999", 121675)]
        internal static PoolRequirement[] Pools() => new[] { new PoolRequirement("any-class-spell", AnyClassSpells(), new[] { "NEUTRAL" }) };
        [OfflinePool("TIME_EVENT_999t", 122549)]
        internal static PoolRequirement[] ReplayPools() => Classes.Select(c => new PoolRequirement("class-spell-" + c, ClassSpells(c),
            new[] { (int)c == 14 ? "DEMONHUNTER" : c.ToString() })).ToArray();
    }
}
