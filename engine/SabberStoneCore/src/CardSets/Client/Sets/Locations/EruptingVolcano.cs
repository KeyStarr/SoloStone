using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EruptingVolcano
    {
        internal const int DbfId = 123163;
        private const int FireSchool = 2;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                Target = LocationTargetKind.None,
                Task = new CustomTask((g, c, s, t, stack) => {
                    var history = g.HistoryFacts.Query;
                    bool fire = history.Plays(history.ThisTurn).Any(f => f.ControllerId == c.Id
                        && f.CardType == CardType.SPELL && f.SpellSchool == FireSchool);
                    // Split damage like Arcane Missiles: one at a time, never at an already-dead enemy. Not spell damage.
                    var shot = ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1);
                    for (int i = 0, shots = fire ? 6 : 3; i < shots; i++)
                        shot.Process(g, c, s, null, new TaskStack());
                    new SetGameTagTask(GameTag.LOCATION_ACTION_COOLDOWN, 1, EntityType.SOURCE).Process(g, c, s, null, new TaskStack());
                })
            } };
        }
    }
}
