using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FrameJob
    {
        internal const int DbfId = 127057;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/CAP_403/v1",
                new CustomTask((g, c, s, t, stack) => {
                    var victims = DiscoverRealRules.Take(c.Opponent.BoardZone.Where(m => !m.IsDormant).ToList(), 2, g);
                    foreach (var victim in victims) victim.Destroy();
                    g.DeathProcessingAndAuraUpdate();
                }), new DiscoverThenContinueTask(DiscoverEntitySource.EnemyDeckMinions, DiscoverEntityAction.PutOnTop)) };
        }
    }
}
