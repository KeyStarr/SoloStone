using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BECostPools
    {
        internal static OfflinePoolFilter MadnessSpell() => OfflinePoolFilter.Random(type: CardType.SPELL);
        internal static readonly OfflinePoolDeclaration[] Declarations = {
            new OfflinePoolDeclaration("BE_870", 131008, new PoolRequirement("attack-cost-spell", MadnessSpell(),
                new[] { "SHAMAN" }, PoolCostBinding.StackNumber, 2))
        };
    }
}
