using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreCostPools
    {
        internal static OfflinePoolFilter OneCostCard() => OfflinePoolFilter.Discover(minCost: 1, maxCost: 1);
        internal static OfflinePoolFilter OtherClassExpensiveSpell() => new OfflinePoolFilter(classMode: PoolClassMode.OtherClass, type: CardType.SPELL, minCost: 5);
        internal static OfflinePoolFilter TwoCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 2, maxCost: 2);
        internal static OfflinePoolFilter FourCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 4, maxCost: 4);
        internal static OfflinePoolFilter EightCostMinion() => OfflinePoolFilter.Discover(type: CardType.MINION, minCost: 8, maxCost: 8);
        internal static OfflinePoolFilter Spell() => OfflinePoolFilter.Discover(type: CardType.SPELL);
        internal static OfflinePoolFilter Demon() => OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.DEMON);
        internal static OfflinePoolFilter Pirate() => OfflinePoolFilter.Random(type: CardType.MINION, race: Race.PIRATE);
        internal static OfflinePoolFilter MageSpell() => new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.MAGE, type: CardType.SPELL);
        internal static OfflinePoolFilter HolySpell() => OfflinePoolFilter.Discover(type: CardType.SPELL, spellSchool: 5);
        internal static OfflinePoolFilter TauntMinion() => OfflinePoolFilter.Discover(type: CardType.MINION, keyword: GameTag.TAUNT);
        internal static OfflinePoolFilter Outcast() => OfflinePoolFilter.Discover(keyword: GameTag.OUTCAST);
        internal static OfflinePoolFilter Beast() => OfflinePoolFilter.Random(type: CardType.MINION, race: Race.BEAST);
        internal static OfflinePoolFilter OtherClassCard() => new OfflinePoolFilter(classMode: PoolClassMode.OtherClass);
        internal static OfflinePoolFilter ShamanSpell() => new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.SHAMAN, type: CardType.SPELL);
        internal static OfflinePoolFilter BattlecryMinion() => OfflinePoolFilter.Discover(type: CardType.MINION, keyword: GameTag.BATTLECRY);
        internal static OfflinePoolFilter Dragon() => OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.DRAGON);
        internal static OfflinePoolFilter Mech() => OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.MECHANICAL);
        internal static readonly OfflinePoolDeclaration[] Declarations = {
            new OfflinePoolDeclaration("CORE_CATA_006", Ulfar.DbfId, new PoolRequirement("death-cost-minion", Ulfar.Filter, new[] { "DRUID" }, SabberStoneCore.Tasks.SimpleTasks.PoolCostBinding.StackNumber, ordinaryCost: 3, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("CORE_CFM_781", 120463, new PoolRequirement("other-class-card", OtherClassCard(), new[] { "ROGUE" })),
            new OfflinePoolDeclaration("CORE_EX1_189", 69672, new PoolRequirement("legendary-minion", OfflinePoolDeclarations.LegendaryMinion(), new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("CORE_GIL_531", 130790, new PoolRequirement("shaman-spell", ShamanSpell(), new[] { "SHAMAN" })),
            new OfflinePoolDeclaration("CORE_GIL_836", 120572, new PoolRequirement("battlecry-minion", BattlecryMinion(), new[] { "SHAMAN" })),
            new OfflinePoolDeclaration("CORE_KAR_057", 130821, new PoolRequirement("spell", Spell(), new[] { "PALADIN" })),
            new OfflinePoolDeclaration("CORE_KAR_062", 120172, new PoolRequirement("dragon", Dragon(), new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("CORE_KAR_069", 69742, new PoolRequirement("other-class-card", OtherClassCard(), new[] { "ROGUE" })),
            new OfflinePoolDeclaration("CORE_KAR_077", 111367, new PoolRequirement("two-cost-minion", TwoCostMinion(), new[] { "PALADIN" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("CORE_LOE_039", 86548, new PoolRequirement("mech", Mech(), new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("CORE_UNG_912", 97376, new PoolRequirement("beast", Beast(), new[] { "HUNTER" })),
            new OfflinePoolDeclaration("CORE_FP1_011", 69692, new PoolRequirement("beast", Beast(), new[] { "HUNTER" })),
            new OfflinePoolDeclaration("CORE_EDR_004_2026", RaptorHerald.DbfId, new PoolRequirement("beast", RaptorHerald.Filter, new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("CORE_EDR_004", RaptorHerald.OriginalDbfId, new PoolRequirement("beast", RaptorHerald.Filter, new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("CORE_BAR_541", 126982, new PoolRequirement("spell", Spell(), new[] { "MAGE" })),
            new OfflinePoolDeclaration("CORE_BT_321", 129167, new PoolRequirement("demon", Demon(), new[] { "DEMONHUNTER" })),
            new OfflinePoolDeclaration("CORE_DRG_024", 127088, new PoolRequirement("pirate", Pirate(), new[] { "WARRIOR" })),
            new OfflinePoolDeclaration("CORE_EDR_001", 117307, new PoolRequirement("mage-spell", MageSpell(), new[] { "MAGE" })),
            new OfflinePoolDeclaration("CORE_ONY_022", 128336, new PoolRequirement("holy-spell", HolySpell(), new[] { "PALADIN" })),
            new OfflinePoolDeclaration("CORE_WON_350", 120468, new PoolRequirement("taunt-minion", TauntMinion(), new[] { "WARRIOR" })),
            new OfflinePoolDeclaration("CORE_YOP_001", 97377, new PoolRequirement("outcast", Outcast(), new[] { "DEMONHUNTER" })),
            new OfflinePoolDeclaration("CORE_WON_096", 120467, new PoolRequirement("one-cost-card", OneCostCard(), new[] { "WARLOCK" })),
            new OfflinePoolDeclaration("CORE_TID_931", 127470, new PoolRequirement("other-class-expensive-spell", OtherClassExpensiveSpell(), new[] { "ROGUE" })),
            new OfflinePoolDeclaration("CORE_REV_308", 118076, new PoolRequirement("two-cost-minion", TwoCostMinion(), new[] { "NEUTRAL" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("CORE_WON_337", 127592, new PoolRequirement("four-cost-minion", FourCostMinion(), new[] { "WARRIOR" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("CORE_AV_107", 127473, new PoolRequirement("eight-cost-minion", EightCostMinion(), new[] { "SHAMAN" }, destination: PoolDestination.Summon))
        };
    }
}
