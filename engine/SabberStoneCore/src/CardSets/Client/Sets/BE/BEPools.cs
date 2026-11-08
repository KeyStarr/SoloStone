using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BEPools
    {
        internal static OfflinePoolFilter ThreeCostSpell() => OfflinePoolFilter.Random(type: CardType.SPELL, minCost: 3, maxCost: 3);

        internal static OfflinePoolFilter Minion() => OfflinePoolFilter.Random(type: CardType.MINION);
        internal static OfflinePoolFilter EightCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 8, maxCost: 8);
        internal static OfflinePoolFilter FiveCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 5, maxCost: 5);
        internal static OfflinePoolFilter OutcastCard() => OfflinePoolFilter.Random(keyword: GameTag.OUTCAST);
        internal static OfflinePoolFilter OneCostMinion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 1, maxCost: 1);
        internal static OfflinePoolFilter Mech() => OfflinePoolFilter.Random(type: CardType.MINION, race: Race.MECHANICAL);
        internal static OfflinePoolFilter ReloadSpell() => OfflinePoolFilter.Discover(type: CardType.SPELL);
        internal static readonly OfflinePoolDeclaration[] Declarations = {
            new OfflinePoolDeclaration("BE_256", AstralMediator.DbfId, new PoolRequirement("ThreeCostSpell", ThreeCostSpell(), new[] { "MAGE" }, destination: PoolDestination.Other)),
            new OfflinePoolDeclaration("BE_555", Reload.DbfId, new PoolRequirement("spell", ReloadSpell(), new[] { "HUNTER" })),
            new OfflinePoolDeclaration("BE_900", Mechacycle.DbfId, new PoolRequirement("remaining-budget-mech", Mech(), new[] { "HUNTER" }, PoolCostBinding.StackNumberMaxCost, 4, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("BE_183", 129839, new PoolRequirement("8-cost-minion", EightCostMinion(), new[] { "PALADIN" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("BE_201", 131200, new PoolRequirement("5-cost-minion", FiveCostMinion(), new[] { "MAGE" }, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("BE_252", 129788, new PoolRequirement("target-cost-minion", Minion(), new[] { "MAGE" }, PoolCostBinding.StackNumber, 2, destination: PoolDestination.Summon)),
            new OfflinePoolDeclaration("BE_264", 130785, new PoolRequirement("outcast-card", OutcastCard(), new[] { "DEMONHUNTER" })),
            new OfflinePoolDeclaration("BE_265", 130520, new PoolRequirement("1-cost-minion", OneCostMinion(), new[] { "DEMONHUNTER" }, destination: PoolDestination.Summon))
        };
    }
}
