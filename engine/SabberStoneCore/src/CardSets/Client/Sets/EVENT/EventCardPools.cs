using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client
{
    internal static class EventCardPools
    {
        internal static OfflinePoolFilter Minions() => OfflinePoolFilter.Random(type: CardType.MINION);
        // Explicit historical bodies: never infer Colossal from a reference tag on Primordial Lord.
        internal static OfflinePoolFilter PastColossal() => new OfflinePoolFilter(scope: PoolScope.Past,
            type: CardType.MINION, cardIds: new[] { "TSC_026", "TSC_029", "TSC_030", "TSC_216", "TSC_937",
                "TSC_639", "TSC_950", "TSC_219", "TSC_962", "TSC_660", "TID_711", "TID_712", "NX2_037" });
        [OfflinePool("CATA_EVENT_000", 126698)]
        internal static PoolRequirement[] Lord() => new[] { new PoolRequirement("past-colossal", PastColossal(), new[] { "NEUTRAL" }, destination: PoolDestination.Hand) };
        [OfflinePool("CATA_EVENT_400", 130005)]
        internal static PoolRequirement[] Crook() => new[] { new PoolRequirement("spent-mana", Minions(), new[] { "DRUID" },
            PoolCostBinding.StackNumber, 3, allowThinExactCost: true, destination: PoolDestination.Summon) };
        [OfflinePool("JAIL_EVENT_102", 130087)]
        internal static PoolRequirement[] Bribe() => new[] {
            new PoolRequirement("summon", Minions().WithCost(2), new[] { "SHAMAN" }, allowThinExactCost: true, destination: PoolDestination.Summon),
            new PoolRequirement("evolve", Minions(), new[] { "SHAMAN" }, PoolCostBinding.StackNumber, 2,
                allowThinExactCost: true, destination: PoolDestination.Transform) };
        internal static Card[] MinionCandidates(Game game, Controller controller, IEntity source, int cost, PoolDestination destination) =>
            OfflineCardPools.Candidates(game, controller.HeroClass, Minions().WithCost(cost), source.Card.AssetId, destination).ToArray();
        internal static Card PickMinion(Game game, Controller controller, IEntity source, int cost, PoolDestination destination)
        {
            var candidates = MinionCandidates(game, controller, source, cost, destination);
            return candidates.Length == 0 ? null : candidates[game.Random.Next(candidates.Length)];
        }
    }
}
