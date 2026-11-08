using SabberStoneCore.Model;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client
{
    internal static class B16AutoplayPools
    {
        [OfflinePool("FIR_907", 115631)]
        internal static PoolRequirement[] AmirdrassilPools() => new[] {
            new PoolRequirement("b16-amirdrassil-minion", Amirdrassil.Pool(), new[] { "DRUID" },
                PoolCostBinding.StackNumber, ordinaryCost: 1, allowThinExactCost: true, destination: PoolDestination.Summon) };
        [OfflinePool("CATA_527t2", 122845)]
        internal static PoolRequirement[] Naga() => new[] {
            new PoolRequirement("b16-nespirah-naga", NespirahEnthralled.Pool(), new[] { "DEMONHUNTER" }) };
        [OfflinePool("TIME_436", 120544)]
        internal static PoolRequirement[] Past() => new[] {
            new PoolRequirement("b16-conflux-random", PastConflux.RandomPool(), new[] { "PRIEST" }, destination: PoolDestination.Summon) };
        [OfflinePool("TIME_436t1", 122262)]
        internal static PoolRequirement[] Present() => new[] {
            new PoolRequirement("b16-conflux-present", PastConflux.DiscoverPool(), new[] { "PRIEST" }, destination: PoolDestination.Summon) };
        [OfflinePool("TIME_436t2", 122263)]
        internal static PoolRequirement[] Future() => new[] {
            new PoolRequirement("b16-conflux-future", PastConflux.DiscoverPool(), new[] { "PRIEST" }, destination: PoolDestination.Summon) };
    }
}
