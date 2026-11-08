using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TimeTravelCostPools
    {
        internal static OfflinePoolFilter Spell() => OfflinePoolFilter.Discover(type: CardType.SPELL);
        internal static OfflinePoolFilter Undead() => OfflinePoolFilter.Random(type: CardType.MINION, race: Race.UNDEAD);
        internal static readonly OfflinePoolDeclaration[] Declarations = {
            new OfflinePoolDeclaration("TIME_612", 121011, new PoolRequirement("class-spell", Spell(), new[] { "DEATHKNIGHT" })),
            new OfflinePoolDeclaration("TIME_615", ForgottenMillennium.DbfId, new PoolRequirement("undead", Undead(), new[] { "DEATHKNIGHT" }))
        };
    }
}
