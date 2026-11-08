using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LeylinePools
    {
        internal static OfflinePoolFilter Minion() => OfflinePoolFilter.Random(type: CardType.MINION);
        internal static readonly OfflinePoolDeclaration[] Declarations = {
            new OfflinePoolDeclaration("MEND_502", 123504, new PoolRequirement("leyline-exact-cost-minion", Minion(),
                new[] { "MAGE" }, PoolCostBinding.StackNumber, 6, destination: PoolDestination.Summon))
        };
    }
}
