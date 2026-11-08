using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SmolderingAscent
    {
        internal const int DbfId = 115640;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Rule: Smoldering Strength's owner-end hand timer; damage is 1/2/3 plus current Spell Damage.
            SmolderingStrength.Display(card);
            card.Power = new Power { Trigger = SmolderingStrength.Expiry(), PowerTask = new CustomTask((g, c, s, t, stack) =>
                new DamageTask(SmolderingStrength.Magnitude((IPlayable)s), EntityType.OP_MINIONS, true).Process(g, c, s, t, stack)) };
        }
    }
}
