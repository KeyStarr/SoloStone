using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SmolderingGrove
    {
        internal const int DbfId = 115635;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Rule: Smoldering Strength's owner-end hand timer; draw 1/2/3, then discard at third owner end.
            SmolderingStrength.Display(card);
            card.Power = new Power { Trigger = SmolderingStrength.Expiry(), PowerTask = new CustomTask((g, c, s, t, stack) =>
                new DrawTask(SmolderingStrength.Magnitude((IPlayable)s)).Process(g, c, s, t, stack)) };
        }
    }
}
