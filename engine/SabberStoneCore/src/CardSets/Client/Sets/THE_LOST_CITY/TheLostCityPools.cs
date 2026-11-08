using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TheLostCityPools
    {
        internal static OfflinePoolFilter LifeCycleMinion() => OfflinePoolFilter.Random(type: CardType.MINION);
        internal static OfflinePoolFilter ScavengerCard() => OfflinePoolFilter.Discover();
        internal static OfflinePoolFilter Murloc() => OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.MURLOC);
        internal static OfflinePoolFilter ExpensiveSpell() => OfflinePoolFilter.Random(type: CardType.SPELL, minCost: 8);
        internal static OfflinePoolFilter MultipleTypes() => OfflinePoolFilter.Random(type: CardType.MINION, multipleMinionTypes: true);
        internal static OfflinePoolFilter ExpensiveTaunt() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 5, keyword: GameTag.TAUNT);
        internal static OfflinePoolFilter TauntAtCost(int cost) => OfflinePoolFilter.Random(type: CardType.MINION, minCost: cost, maxCost: cost, keyword: GameTag.TAUNT);
        internal static OfflinePoolFilter OneCost(CardType type) => OfflinePoolFilter.Random(type: type, minCost: 1, maxCost: 1);
        internal static OfflinePoolFilter FourCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 4, maxCost: 4);
        internal static OfflinePoolFilter SpellSchool(int school) => OfflinePoolFilter.Random(type: CardType.SPELL, spellSchool: school);
        internal static OfflinePoolFilter Biome() => OfflinePoolFilter.Discover(type: CardType.MINION, minCost: 1, maxCost: 1);
        internal static OfflinePoolFilter Terror() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 2, maxCost: 2);

        internal static OfflinePoolFilter LegendaryMinionDiscover() => OfflinePoolFilter.Discover(type: CardType.MINION, rarity: Rarity.LEGENDARY);
        internal static OfflinePoolFilter ThreeCostMinionDiscover() => OfflinePoolFilter.Discover(type: CardType.MINION, minCost: 3, maxCost: 3);
        internal static OfflinePoolFilter UndeadDiscover() => OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.UNDEAD);
        internal static OfflinePoolFilter SameRarity(Rarity rarity) => OfflinePoolFilter.Discover(rarity: rarity);

        internal static OfflinePoolFilter FrostRuneDiscover() => OfflinePoolFilter.Discover(keyword: (GameTag)2197);
        internal static OfflinePoolFilter FelSpellDiscover() => OfflinePoolFilter.Discover(type: CardType.SPELL, spellSchool: 7);
        internal static OfflinePoolFilter TwoCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 2, maxCost: 2);

        internal static OfflinePoolFilter OtherClassWeapon() => new OfflinePoolFilter(classMode: PoolClassMode.OtherClass, type: CardType.WEAPON);
        internal static OfflinePoolFilter ThreeCostBeast() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 3, maxCost: 3, race: Race.BEAST);
        internal static readonly OfflinePoolDeclaration[] Declarations = {
            new OfflinePoolDeclaration("DINO_422", Ankylodon.DbfId, new PoolRequirement("3-cost-beast", ThreeCostBeast(), new[] { "HUNTER" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("TLC_235", LifeCycle.DbfId, new PoolRequirement("same-printed-cost-minion", LifeCycleMinion(),
                new[] { "DRUID" }, SabberStoneCore.Tasks.SimpleTasks.PoolCostBinding.StackNumber, ordinaryCost: 2, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("TLC_516", NefersetWeaponsmith.DbfId, new PoolRequirement("other-class-weapon", OtherClassWeapon(), new[] { "ROGUE" })),
            new OfflinePoolDeclaration("TLC_461", ScrappyScavenger.DbfId, new PoolRequirement("remaining-mana-card", ScavengerCard(), new[] { "MAGE" }, PoolCostBinding.StackNumber, 9)),
            new OfflinePoolDeclaration("TLC_815", GravedawnVoidbulb.DbfId,
                new PoolRequirement("4-cost-minion", FourCostMinion(), new[] { "PRIEST" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("TLC_449", BloodpetalBiome.DbfId, new PoolRequirement("B1", Biome(), new[] { "WARLOCK" })),
            new OfflinePoolDeclaration("TLC_469", TunnelTerror.DbfId, new PoolRequirement("TT2", Terror(), new[] { "WARLOCK" })),
            new OfflinePoolDeclaration("TLC_514", MerchantOfLegend.DbfId, new PoolRequirement("legendary-minion", LegendaryMinionDiscover(), new[] { "ROGUE" })),
            new OfflinePoolDeclaration("TLC_435", CryptMap.DbfId, new PoolRequirement("frost-rune", FrostRuneDiscover(), new[] { "DEATHKNIGHT" })),
            new OfflinePoolDeclaration("TLC_900", HiveMap.DbfId, new PoolRequirement("fel-spell", FelSpellDiscover(), new[] { "DEMONHUNTER" })),
            new OfflinePoolDeclaration("TLC_462", UnearthedArtifacts.DbfId,
                new PoolRequirement("2-cost-minion", TwoCostMinion(), new[] { "MAGE" }, destination: PoolDestination.Summon),
                new PoolRequirement("4-cost-minion", FourCostMinion(), new[] { "MAGE" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("DINO_424", HerosWelcome.DbfId, new PoolRequirement("legendary-minion", LegendaryMinionDiscover(), new[] { "PALADIN" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("DINO_426", RitualOfLife.DbfId, new PoolRequirement("3-cost-minion", ThreeCostMinionDiscover(), new[] { "PRIEST" })),
            new OfflinePoolDeclaration("TLC_434", Paleomancy.DbfId, new PoolRequirement("undead", UndeadDiscover(), new[] { "DEATHKNIGHT" })),
            new OfflinePoolDeclaration("TLC_109", RelicMiner.DbfId,
                new PoolRequirement("common", SameRarity(Rarity.COMMON), new[] { "NEUTRAL" }),
                new PoolRequirement("rare", SameRarity(Rarity.RARE), new[] { "NEUTRAL" }),
                new PoolRequirement("epic", SameRarity(Rarity.EPIC), new[] { "NEUTRAL" }),
                new PoolRequirement("legendary", SameRarity(Rarity.LEGENDARY), new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("TLC_442", SubmergedMap.DbfId, new PoolRequirement("murloc", Murloc(), new[] { "PALADIN" })),
            new OfflinePoolDeclaration("TLC_334", RelicOfKings.DbfId, new PoolRequirement("any-class-8-plus-cost-spell", ExpensiveSpell(), new[] { "MAGE" })),
            new OfflinePoolDeclaration("DINO_412", Tortotem.DbfId, new PoolRequirement("multiple-minion-types", MultipleTypes(), new[] { "SHAMAN" })),
            new OfflinePoolDeclaration("DINO_431", Atlasaurus.DbfId, new PoolRequirement("5-plus-cost-taunt", ExpensiveTaunt(), new[] { "PRIEST" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("DINO_433", GuardDuty.DbfId,
                new PoolRequirement("6-cost-taunt", TauntAtCost(6), new[] { "WARRIOR" }, destination: PoolDestination.Summon),
                new PoolRequirement("4-cost-taunt", TauntAtCost(4), new[] { "WARRIOR" }, destination: PoolDestination.Summon),
                new PoolRequirement("2-cost-taunt", TauntAtCost(2), new[] { "WARRIOR" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("DINO_434", RaptorNestNurse.DbfId,
                new PoolRequirement("1-cost-minion", OneCost(CardType.MINION), new[] { "HUNTER" }),
                new PoolRequirement("1-cost-spell", OneCost(CardType.SPELL), new[] { "HUNTER" })),
            new OfflinePoolDeclaration("TLC_477", ThreshridersBlessing.DbfId, new PoolRequirement("4-cost-minion", FourCostMinion(), new[] { "PALADIN" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("TLC_814", TwilightMender.DbfId,
                new PoolRequirement("holy-spell", SpellSchool(5), new[] { "PRIEST" }),
                new PoolRequirement("shadow-spell", SpellSchool(6), new[] { "PRIEST" }))
        };
    }
}
