using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TimeTravelPools
    {
        internal static OfflinePoolFilter Secrets() => OfflinePoolFilter.Random(type: CardType.SPELL, keyword: GameTag.SECRET);

        internal static OfflinePoolFilter BronzeMinion() => new OfflinePoolFilter(classMode: PoolClassMode.OtherClass, type: CardType.MINION);
        internal static OfflinePoolFilter WindrunnerSpell() => OfflinePoolFilter.Discover(type: CardType.SPELL);
        internal static OfflinePoolFilter ShadowSpell() => OfflinePoolFilter.Random(type: CardType.SPELL, spellSchool: 6);
        internal static OfflinePoolFilter OneCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 1, maxCost: 1);
        internal static OfflinePoolFilter TwoCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 2, maxCost: 2);

        internal static OfflinePoolFilter AnyMinion() => OfflinePoolFilter.Random(type: CardType.MINION);
        internal static OfflinePoolFilter BudgetMinion(int remaining) => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 0, maxCost: remaining);
        internal static OfflinePoolFilter HolySpell() => OfflinePoolFilter.Random(type: CardType.SPELL, spellSchool: 5);
        internal static OfflinePoolFilter NatureSpell() => OfflinePoolFilter.Random(type: CardType.SPELL, spellSchool: 4);
        internal static OfflinePoolFilter AnyWeapon() => OfflinePoolFilter.Random(type: CardType.WEAPON);
        internal static OfflinePoolFilter ThreeCostBeast() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 3, maxCost: 3, race: Race.BEAST);
        internal static OfflinePoolFilter EightCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 8, maxCost: 8);
        // "A random minion" is any class (offline pool policy); Discover is class + neutral.
        internal static OfflinePoolFilter DeathrattleMinion() => OfflinePoolFilter.Random(type: CardType.MINION, keyword: GameTag.DEATHRATTLE);
        internal static OfflinePoolFilter PastMinion() => OfflinePoolFilter.Random(type: CardType.MINION, scope: PoolScope.Past);
        internal static OfflinePoolFilter DiscoverMinion() => OfflinePoolFilter.Discover(type: CardType.MINION);

        internal static OfflinePoolFilter GuessFallback() => OfflinePoolFilter.Random();
        internal static OfflinePoolFilter MurozondDragon() => OfflinePoolFilter.Random(type:CardType.MINION,race:Race.DRAGON);
        internal static readonly OfflinePoolDeclaration[] Declarations = {
            new OfflinePoolDeclaration("TIME_860", FacelessEnigma.DbfId, new PoolRequirement("Secrets", Secrets(), new[] { "MAGE" }, destination: PoolDestination.Other)),
            new OfflinePoolDeclaration("TIME_041", FuturisticForefather.DbfId, new PoolRequirement("guess-fallback", GuessFallback(), new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("END_037",EndtimeMurozond.DbfId,new PoolRequirement("murozond-dragon",MurozondDragon(),new[] { "NEUTRAL" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("END_000p", 121243, new PoolRequirement("other-class-minion", BronzeMinion(), new[] { "ROGUE" })),
            new OfflinePoolDeclaration("TIME_038", MisterClocksworth.DbfId, new PoolRequirement("legendary-minion", MisterClocksworth.Filter, new[] { "NEUTRAL" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("TIME_038t1", 120982, new PoolRequirement("legendary-minion", MisterClocksworth.Filter, new[] { "NEUTRAL" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("TIME_038t2", 120983, new PoolRequirement("legendary-minion", MisterClocksworth.Filter, new[] { "NEUTRAL" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("TIME_038t3", 120984, new PoolRequirement("legendary-minion", MisterClocksworth.Filter, new[] { "NEUTRAL" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("TIME_000", SemiStablePortal.DbfId, new PoolRequirement("any-minion", AnyMinion(), new[] { "MAGE" })),
            new OfflinePoolDeclaration("TIME_014", InstantMultiverse.DbfId, Enumerable.Range(1, 12)
                .Select(remaining => new PoolRequirement("budget-" + remaining, BudgetMinion(remaining), new[] { "SHAMAN" }, destination: PoolDestination.Summon)).ToArray()),
            new OfflinePoolDeclaration("TIME_018", MendTheTimeline.DbfId, new PoolRequirement("holy-spell", HolySpell(), new[] { "PALADIN" })),
            new OfflinePoolDeclaration("TIME_033", DruidOfRegrowth.DbfId, new PoolRequirement("nature-spell", NatureSpell(), new[] { "DRUID" }, destination: PoolDestination.Other)),
            new OfflinePoolDeclaration("TIME_034", StadiumAnnouncer.DbfId, new PoolRequirement("any-weapon", AnyWeapon(), new[] { "WARRIOR" }, destination: PoolDestination.Other)),
            new OfflinePoolDeclaration("TIME_602", Wormhole.DbfId, new PoolRequirement("3-cost-beast", ThreeCostBeast(), new[] { "HUNTER" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("TIME_002", AeonWizard.DbfId, PoolRequirement.PlayableClasses.Select(hero =>
                new PoolRequirement("class-spell-" + hero, AeonWizard.ClassSpells(PoolRequirement.HeroClass(hero)), new[] { "NEUTRAL" })).ToArray()),
            new OfflinePoolDeclaration("TIME_035", TimeMachine.DbfId, new PoolRequirement("rewind-card", TimeMachine.Filter, new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("END_036", Morchie.DbfId, new PoolRequirement("rewind-card", TimeMachine.Filter, new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("TIME_609t1", RangerCaptainAlleria.DbfId, new PoolRequirement("spell", WindrunnerSpell(), new[] { "HUNTER" })),
            new OfflinePoolDeclaration("TIME_058", PaltryFlutterwing.DbfId, new PoolRequirement("PF2", TwoCostMinion(), new[] { "NEUTRAL" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("END_020", EternalToil.DbfId, new PoolRequirement("one-cost-minion", OneCostMinion(), new[] { "WARLOCK", "WARRIOR" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("TIME_102", Circadiamancer.DbfId, new PoolRequirement("eight-cost-minion", EightCostMinion(), new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("TIME_712", Dethrone.DbfId, new PoolRequirement("eight-cost-minion", EightCostMinion(), new[] { "ROGUE" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("END_029", VoodooTotem.DbfId, new PoolRequirement("shadow-spell", ShadowSpell(), new[] { "PRIEST", "SHAMAN" })),
            new OfflinePoolDeclaration("TIME_872", UndefeatedChampion.DbfId, new PoolRequirement("one-cost-minion", OneCostMinion(), new[] { "WARRIOR" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("END_013", BrutishEndmaw.DbfId, new PoolRequirement("1-cost-minion", BrutishEndmaw.Filter, new[] { "HUNTER", "PALADIN" })),
            new OfflinePoolDeclaration("END_015", TrienniumRex.DbfId, new PoolRequirement("deathrattle-minion", DeathrattleMinion(), new[] { "HUNTER" })),
            new OfflinePoolDeclaration("TIME_052", AmberWarden.DbfId, new PoolRequirement("past-minion", PastMinion(), new[] { "NEUTRAL" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("TIME_448", Solitude.DbfId, new PoolRequirement("discover-minion", DiscoverMinion(), new[] { "DEMONHUNTER" }))
        };
    }
}
