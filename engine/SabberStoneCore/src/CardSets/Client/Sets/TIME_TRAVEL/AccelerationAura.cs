using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AccelerationAura
    {
        [StandardCard("END_011", 121150)]
        internal static void Configure(Card card) => AuraSpellRules.Declare(card,
            clock: AuraSpellClock.OwnerStart, effect: new TempManaTask(1));
    }
}
