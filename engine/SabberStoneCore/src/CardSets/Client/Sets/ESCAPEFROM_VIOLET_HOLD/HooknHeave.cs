using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HooknHeave
    {
        internal const int DbfId = 127017;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/CAP_105/v1",
                new DiscoverThenContinueTask(EscapeFromVioletHoldPools.Pirate()),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.LastOrDefault();
                    if (chosen != null && Generic.RemoveFromZone(c, chosen)) Generic.AddHandPhase(c, chosen);
                    // The pinned export's tag 1452 points to CAP_107t, not the obsolete CAP_105t.
                    var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                    ctx.Summon("CAP_107t");
                    ctx.Summon("CAP_107t");
                })) };
        }
    }
}
