using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Bombardment
    {
        internal const int DbfId = 129822;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(ControlsMech);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                for (int i = 0; i < 2; i++)
                {
                    if (i == 1 && !ControlsMech((IPlayable)s)) break;
                    // Deaths wait for the spell to end; the next random destroy must skip pending deaths.
                    var targets = new TaskStack { Playables = c.Opponent.BoardZone.GetAll(m => !m.IsDead).Cast<IPlayable>().ToList() };
                    if (targets.Playables.Count > 0)
                    {
                        new RandomTask(1, EntityType.STACK).Process(g, c, s, null, targets);
                        new DestroyTask(EntityType.STACK).Process(g, c, s, null, targets);
                    }
                    new DamageTask(3, EntityType.OP_HERO, true).Process(g, c, s, null, stack);
                }
            }) };
        }

        private static bool ControlsMech(IPlayable source) => source.Controller.BoardZone.GetAll()
            .Any(m => m != source && m.IsRace(Race.MECHANICAL));
    }
}
