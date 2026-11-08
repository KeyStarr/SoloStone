using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CosmicManifestationsPools
    {
        internal static OfflinePoolFilter DemonHunterSpell() => new OfflinePoolFilter(
            classMode: PoolClassMode.Exact, exactClass: CardClass.DEMONHUNTER, type: CardType.SPELL);

        internal static readonly OfflinePoolDeclaration[] Declarations = {
            new OfflinePoolDeclaration("JAIL_892", 127159,
                new PoolRequirement("demonhunter-spell", DemonHunterSpell(), new[] { "DEMONHUNTER" }, destination: PoolDestination.Deck))
        };
    }
}
