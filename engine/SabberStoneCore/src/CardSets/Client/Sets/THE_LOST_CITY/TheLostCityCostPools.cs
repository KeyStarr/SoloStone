using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TheLostCityCostPools
    {
        internal static OfflinePoolFilter FelSpell() => OfflinePoolFilter.Random(type: CardType.SPELL, spellSchool: 7);
        internal static readonly OfflinePoolDeclaration[] Declarations = {
            new OfflinePoolDeclaration("TLC_467", 118277, new PoolRequirement("fel-spell", FelSpell(), new[] { "WARLOCK" }))
        };
    }
}
