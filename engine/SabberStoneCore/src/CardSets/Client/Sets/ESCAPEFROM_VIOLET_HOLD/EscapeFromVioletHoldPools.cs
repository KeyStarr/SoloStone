using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EscapeFromVioletHoldPools
    {
        internal static OfflinePoolFilter MageSecret() => new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.MAGE, type: CardType.SPELL, keyword: GameTag.SECRET);

        internal static OfflinePoolFilter ChefTaunt(int cost) => OfflinePoolFilter.Random(type: CardType.MINION, minCost: cost, maxCost: cost, keyword: GameTag.TAUNT);
        internal static OfflinePoolFilter ManastormMinion() => OfflinePoolFilter.Random(type: CardType.MINION);
        internal static OfflinePoolFilter Spell() => OfflinePoolFilter.Random(type: CardType.SPELL);
        internal static OfflinePoolFilter FiveCostBeast() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 5, maxCost: 5, race: Race.BEAST);
        internal static OfflinePoolFilter Weapon() => OfflinePoolFilter.Random(type: CardType.WEAPON);
        internal static OfflinePoolFilter Pirate() => OfflinePoolFilter.Discover(type: CardType.MINION, race: Race.PIRATE);
        internal static OfflinePoolFilter ExpensiveMinion() => OfflinePoolFilter.Discover(type: CardType.MINION, minCost: 5);

        internal static OfflinePoolFilter BattlecryMinion() => OfflinePoolFilter.Random(type: CardType.MINION, keyword: GameTag.BATTLECRY);
        internal static OfflinePoolFilter LegendaryMinion() => OfflinePoolFilter.Random(type: CardType.MINION, rarity: Rarity.LEGENDARY);
        internal static OfflinePoolFilter FourCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 4, maxCost: 4);
        internal static OfflinePoolFilter OneCostDeathrattle() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 1, maxCost: 1, keyword: GameTag.DEATHRATTLE);
        internal static OfflinePoolFilter SixCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 6, maxCost: 6);
        internal static OfflinePoolFilter DruidCard() => new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.DRUID);
        internal static OfflinePoolFilter FourCostSpell() => OfflinePoolFilter.Random(type: CardType.SPELL, minCost: 4, maxCost: 4);
        internal static OfflinePoolFilter ExpensiveSpell() => OfflinePoolFilter.Random(type: CardType.SPELL, minCost: 5);

        internal static OfflinePoolFilter ArchitectSpell() => OfflinePoolFilter.Discover(type: CardType.SPELL, minCost: 5);
        internal static readonly OfflinePoolDeclaration[] Declarations = {
            new OfflinePoolDeclaration("JAIL_321", TricksyImproviser.DbfId, new PoolRequirement("MageSecret", MageSecret(), new[] { "MAGE" }, destination: PoolDestination.Other)),
            new OfflinePoolDeclaration("JAIL_986", FranticForger.DbfId, new PoolRequirement("affordable-spell", FranticForger.Filter, new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("JAIL_123",BreakoutArchitect.DbfId,new PoolRequirement("discover-expensive-spell",ArchitectSpell(),new[] { "MAGE" })),
            new OfflinePoolDeclaration("JAIL_507", SpitefulChef.DbfId,
                new PoolRequirement("2-cost-taunt", ChefTaunt(2), new[] { "DRUID" }, destination: PoolDestination.Summon),
                new PoolRequirement("6-cost-taunt", ChefTaunt(6), new[] { "DRUID" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("JAIL_451", BloodClone.DbfId, new PoolRequirement("five-cost-minion", BloodClone.Filter, new[] { "DEATHKNIGHT" })),
            new OfflinePoolDeclaration("JAIL_122", JailhouseManastorm.DbfId, new PoolRequirement("spell-cost-minion", ManastormMinion(), new[] { "MAGE" }, PoolCostBinding.StackNumber, 4, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("JAIL_313", BootlegAlchemist.DbfId,
                new PoolRequirement("hand-cost-plus-five-spell", Spell(), new[] { "MAGE" }, PoolCostBinding.StackNumber, 6, destination: PoolDestination.Transform)),
            new OfflinePoolDeclaration("JAIL_879", BeastTripwire.DbfId,
                new PoolRequirement("5-cost-beast", FiveCostBeast(), new[] { "HUNTER" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("JAIL_460", ConcealingConfection.DbfId,
                new PoolRequirement("weapon", Weapon(), new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("CAP_105", HooknHeave.DbfId,
                new PoolRequirement("pirate", Pirate(), new[] { "WARRIOR" })),
            new OfflinePoolDeclaration("CAP_407", WantedPoster.DbfId,
                new PoolRequirement("5-or-more-cost-minion", ExpensiveMinion(), new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("JAIL_407", VanessatheRingleader.DbfId,
                new PoolRequirement("battlecry-minion", BattlecryMinion(), new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("JAIL_448", KarovtheBroken.DbfId,
                new PoolRequirement("legendary-minion", LegendaryMinion(), new[] { "PRIEST" })),
            new OfflinePoolDeclaration("JAIL_876", DigforFreedom.DbfId,
                new PoolRequirement("4-cost-minion", FourCostMinion(), new[] { "HUNTER" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("JAIL_878", GuardDog.DbfId,
                new PoolRequirement("1-cost-deathrattle-minion", OneCostDeathrattle(), new[] { "HUNTER" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("JAIL_912", Soothsayer.DbfId,
                new PoolRequirement("6-cost-minion", SixCostMinion(), new[] { "PRIEST" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("JAIL_201", SecretIngredient.DbfId,
                new PoolRequirement("druid-card", DruidCard(), new[] { "DRUID" })),
            new OfflinePoolDeclaration("JAIL_706", ThiefsTools.DbfId,
                new PoolRequirement("4-cost-spell", FourCostSpell(), new[] { "ROGUE" })),
            new OfflinePoolDeclaration("JAIL_806", Hexmarshal.DbfId,
                new PoolRequirement("5-or-more-cost-spell", ExpensiveSpell(), new[] { "SHAMAN" }))
        };
    }
}
