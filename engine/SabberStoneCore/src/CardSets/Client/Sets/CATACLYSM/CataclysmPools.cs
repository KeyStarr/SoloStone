using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CataclysmPools
    {
        internal static OfflinePoolFilter CostMinion(int cost) => OfflinePoolFilter.Random(type: CardType.MINION, minCost: cost, maxCost: cost);
        internal static OfflinePoolFilter NatureSpell() => OfflinePoolFilter.Random(type: CardType.SPELL, spellSchool: 4);
        internal static OfflinePoolFilter HolySpell() => OfflinePoolFilter.Random(type: CardType.SPELL, spellSchool: 5);
        internal static OfflinePoolFilter CheapDragon() => OfflinePoolFilter.Random(type: CardType.MINION, maxCost: 3, race: Race.DRAGON);
        internal static OfflinePoolFilter Dragon() => OfflinePoolFilter.Random(type: CardType.MINION, race: Race.DRAGON);
        internal static OfflinePoolFilter ExpensiveMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 8);
        internal static OfflinePoolFilter FourCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 4, maxCost: 4);

        internal static OfflinePoolFilter HeraldMinion() => OfflinePoolFilter.Random(type: CardType.MINION);
        internal static OfflinePoolFilter HeraldOtherClassSpell() => new OfflinePoolFilter(classMode: PoolClassMode.OtherClass, type: CardType.SPELL);
        internal static OfflinePoolFilter AttackCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION);
        internal static OfflinePoolFilter FireSpell() => OfflinePoolFilter.Random(type: CardType.SPELL, spellSchool: 2);

        internal static readonly OfflinePoolDeclaration[] Declarations = {
            new OfflinePoolDeclaration("MEND_046", 123493, new PoolRequirement("nature-spell", NatureSpell(), new[] { "DRUID" })),
            new OfflinePoolDeclaration("CATA_154t",125451,new PoolRequirement("wing-other-class-spell",HeraldOtherClassSpell(),new[] { "ROGUE" })),
            new OfflinePoolDeclaration("CATA_154t1",125503,new PoolRequirement("wing-other-class-spell",HeraldOtherClassSpell(),new[] { "ROGUE" })),
            new OfflinePoolDeclaration("MEND_300", 123486, new PoolRequirement("companion-cost-beast", CompanionRules.BeastPool(), new[] { "HUNTER" }, PoolCostBinding.StackNumber, 4, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("MEND_303", 123491, new PoolRequirement("companion-cost-beast", CompanionRules.BeastPool(), new[] { "HUNTER" }, PoolCostBinding.StackNumber, 4, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("MEND_307", 123513, new PoolRequirement("companion-cost-beast", CompanionRules.BeastPool(), new[] { "HUNTER" }, PoolCostBinding.StackNumber, 5, destination: PoolDestination.Summon)),

            new OfflinePoolDeclaration("CATA_780t", 126983, new PoolRequirement("herald-cost-minion", HeraldMinion(), new[] { "DEATHKNIGHT" }, PoolCostBinding.StackNumber, 2)),
            new OfflinePoolDeclaration("CATA_158t", 127003, new PoolRequirement("herald-other-class-spell", HeraldOtherClassSpell(), new[] { "ROGUE" })),
            new OfflinePoolDeclaration("CATA_153", 125442, new PoolRequirement("attack-cost-minion", AttackCostMinion(), new[] { "SHAMAN" }, SabberStoneCore.Tasks.SimpleTasks.PoolCostBinding.StackNumber, ordinaryCost: 4)),
            new OfflinePoolDeclaration("CATA_488", 123665, new PoolRequirement("fire-spell", FireSpell(), new[] { "MAGE" })),
            new OfflinePoolDeclaration("CATA_499", 122831, new PoolRequirement("1-cost-minion", CostMinion(1), new[] { "WARLOCK" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("CATA_569", 123001,
                new PoolRequirement("3-cost-minion", CostMinion(3), new[] { "SHAMAN" }, destination: PoolDestination.Summon),
                new PoolRequirement("2-cost-minion", CostMinion(2), new[] { "SHAMAN" }, destination: PoolDestination.Summon),
                new PoolRequirement("1-cost-minion", CostMinion(1), new[] { "SHAMAN" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("MEND_042", 123485, new PoolRequirement("8-cost-minion", CostMinion(8), new[] { "DRUID" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("CATA_474", 123112, new PoolRequirement("holy-spell", HolySpell(), new[] { "PALADIN" })),
            new OfflinePoolDeclaration("CATA_556", 122933, new PoolRequirement("dragon-cost-at-most-3", CheapDragon(), new[] { "NEUTRAL" })),
            new OfflinePoolDeclaration("MEND_045", 123482, new PoolRequirement("dragon", Dragon(), new[] { "DRUID" })),
            new OfflinePoolDeclaration("CATA_136", 122975, new PoolRequirement("minion-cost-at-least-8", ExpensiveMinion(), new[] { "DRUID" }, destination: PoolDestination.Deck)),
            new OfflinePoolDeclaration("CATA_471", 122879, new PoolRequirement("4-cost-minion", FourCostMinion(), new[] { "DEATHKNIGHT" }, destination: PoolDestination.Summon))
        };
    }
}
