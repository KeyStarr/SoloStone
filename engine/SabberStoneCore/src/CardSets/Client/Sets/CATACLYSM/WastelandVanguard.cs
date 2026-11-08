using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WastelandVanguard
    {
        internal const int DbfId = 123490;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var missile = ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1);
                bool killed = false;
                for (int i = 0; i < 3; i++)
                {
                    var victims = new TaskStack();
                    missile.Process(g, c, s, null, victims);
                    killed |= victims.Playables.OfType<Minion>().Any(m => m.IsDead);
                }
                if (killed)
                    for (int i = 0; i < 3; i++) missile.Process(g, c, s, null, new TaskStack());
            }) };
        }

    }
}
