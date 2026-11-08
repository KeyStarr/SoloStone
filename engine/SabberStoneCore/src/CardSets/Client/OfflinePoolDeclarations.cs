using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client
{
    /// <summary>Declarations are independent of registration. Recipes reuse these named factories.</summary>
    public static class OfflinePoolDeclarations
    {
        public static OfflinePoolFilter AnyClassOneCostSpell() => OfflinePoolFilter.Random(type: CardType.SPELL, minCost: 1, maxCost: 1);
        public static OfflinePoolFilter FourCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 4, maxCost: 4);
        public static OfflinePoolFilter OneCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 1, maxCost: 1);
        public static OfflinePoolFilter LegendaryMinion() => OfflinePoolFilter.Random(type: CardType.MINION, rarity: Rarity.LEGENDARY);
        public static OfflinePoolFilter ThreeCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 3, maxCost: 3);
        public static OfflinePoolFilter FrostSpell() => OfflinePoolFilter.Random(type: CardType.SPELL, spellSchool: 3);
        public static OfflinePoolFilter ShadowSpell() => OfflinePoolFilter.Random(type: CardType.SPELL, spellSchool: 6);
        public static OfflinePoolFilter NatureSpell() => OfflinePoolFilter.Random(type: CardType.SPELL, spellSchool: 4);
        public static OfflinePoolFilter PastFiveCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 5, maxCost: 5, scope: PoolScope.Past);
        public static OfflinePoolFilter PaladinCard() => new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.PALADIN);
        public static OfflinePoolFilter DruidSpell() => new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.DRUID, type: CardType.SPELL);

        public static OfflinePoolFilter DeathsAdvanceSpell() => OfflinePoolFilter.Discover(type: CardType.SPELL);
        public static OfflinePoolFilter BloodRuneCard() => OfflinePoolFilter.Random(keyword: (GameTag)2196);
        public static OfflinePoolFilter FrostRuneCard() => OfflinePoolFilter.Random(keyword: (GameTag)2197);
        public static OfflinePoolFilter ExpensiveDeathrattleMinion() => OfflinePoolFilter.Discover(type: CardType.MINION, minCost: 5, keyword: GameTag.DEATHRATTLE);

        // Pinned export 37.0.0.254477 cardClasses. Repeated picks share one requirement, distinct pools do not.
        public static IReadOnlyList<OfflinePoolDeclaration> Pilots { get; } = Array.AsReadOnly(new[] {
            new OfflinePoolDeclaration("CATA_484", 123398, new PoolRequirement("any-class-1-cost-spell", AnyClassOneCostSpell(), new[] { "MAGE" })),
            new OfflinePoolDeclaration("CATA_723", 122641, new PoolRequirement("4-cost-minion", FourCostMinion(), new[] { "NEUTRAL" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("BE_854", 129892, new PoolRequirement("3-cost-minion", ThreeCostMinion(), new[] { "DRUID" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("JAIL_125", 126356, new PoolRequirement("frost-spell", FrostSpell(), new[] { "MAGE" })),
            new OfflinePoolDeclaration("BE_220", 129256, new PoolRequirement("shadow-spell", ShadowSpell(), new[] { "PRIEST" })),
            new OfflinePoolDeclaration("EDR_530", 115364, new PoolRequirement("nature-spell", NatureSpell(), new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("TIME_040", 120782, new PoolRequirement("past-5-cost-minion", PastFiveCostMinion(), new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("JAIL_328", 126618, new PoolRequirement("paladin-card", PaladinCard(), new[] { "PALADIN" })),
            new OfflinePoolDeclaration("EDR_848", 114914, new PoolRequirement("druid-spell", DruidSpell(), new[] { "DRUID" }))
        });

        public static IReadOnlyList<OfflinePoolDeclaration> DeathKnight { get; } = Array.AsReadOnly(new[] {
            new OfflinePoolDeclaration("DINO_415", 118555, new PoolRequirement("expensive-deathrattle-minion", ExpensiveDeathrattleMinion(), new[] { "DEATHKNIGHT" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("CORE_CATA_009", 127412, new PoolRequirement("discover-spell", DeathsAdvanceSpell(), new[] { "DEATHKNIGHT" })),
            new OfflinePoolDeclaration("CORE_RLK_066", 111320, new PoolRequirement("blood-rune-card", BloodRuneCard(), new[] { "DEATHKNIGHT" })),
            new OfflinePoolDeclaration("RLK_025", 78325, new PoolRequirement("frost-rune-card", FrostRuneCard(), new[] { "DEATHKNIGHT" })),
            new OfflinePoolDeclaration("BE_749", 131157, new PoolRequirement("1-cost-minion", OneCostMinion(), new[] { "DEATHKNIGHT" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("TIME_613", 120481, new PoolRequirement("legendary-minion", LegendaryMinion(), new[] { "DEATHKNIGHT" }))
        });

        // Built on first use: set pool classes call the factories above from their own static initializers,
        // so an eager aggregate here would read their Declarations while they are still null (Unity/Mono init order).
        private static readonly Lazy<IReadOnlyList<OfflinePoolDeclaration>> _cards = new Lazy<IReadOnlyList<OfflinePoolDeclaration>>(BuildCards);

        public static IReadOnlyList<OfflinePoolDeclaration> Cards => _cards.Value;

        private static IReadOnlyList<OfflinePoolDeclaration> BuildCards()
        {
            return OfflinePoolAttribute.Collect(Pilots
                .Concat(DeathKnight)
                .Concat(EmeraldDreamPools.Declarations)
                .Concat(TheLostCityPools.Declarations)
                .Concat(TimeTravelPools.Declarations)
                .Concat(CataclysmPools.Declarations)
                .Concat(LeylinePools.Declarations)
                .Concat(EscapeFromVioletHoldPools.Declarations)
                .Concat(BEPools.Declarations)
                .Concat(CosmicManifestationsPools.Declarations)
                .Concat(CoreCostPools.Declarations)
                .Concat(TheLostCityCostPools.Declarations)
                .Concat(BECostPools.Declarations)
                .Concat(TimeTravelCostPools.Declarations), typeof(OfflinePoolDeclarations).Assembly.GetTypes());
        }
    }
}
