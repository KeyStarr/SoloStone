using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Rajh
    {
        // shortcut: audited executable offline subset; extend only after auditing each split payload for hero ownership.
        private static readonly string[] Catalog = { "TIME_428", "TIME_100", "CATA_133", "CATA_475", "CATA_476", "CATA_999", "CORE_EX1_559" };
        internal static OfflinePoolFilter Pool => new OfflinePoolFilter(type: CardType.MINION, cardIds: Catalog);
        internal static bool Supports(Card card) => Catalog.Contains(card.Id, StringComparer.Ordinal);
        [StandardCard("BE_004",132353)]
        internal static void Configure(Card card) => AssembleRules.Configure(card);
        [StandardCard("BE_004p",132368)]
        internal static void Power(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power(); }
        [StandardCard("BE_044e",134955)]
        internal static void Assembly(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power(); }
    }
}

namespace SabberStoneCore.CardSets.Client
{
    internal static class RajhPools
    {
        [OfflinePool("BE_004",132353)]
        internal static PoolRequirement[] Pools() => new[] { new PoolRequirement("rajh-passive-components", Sets.Rajh.Pool, new[] { "PALADIN" }, destination: PoolDestination.Other) };
    }
}
