using System;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    /// <summary>
    /// Shared rules for "steal N Health" and "-N Health" effects (#198). Retail gives the victim a -Health enchantment: it lowers
    /// the maximum (HEALTH) and never touches Damage, so current Health falls by the full amount and a damaged minion can die.
    /// A steal is capped at the victim's current Health, not its maximum. Nothing deals damage, so a victim left at 0 Health
    /// has to be marked for the next death phase here.
    /// </summary>
    internal static class HealthLossRules
    {
        /// <summary>How much of a steal of <paramref name="requested"/> Health the victim can give: at most its current Health, never negative.</summary>
        internal static int StealAmount(ICharacter victim, int requested) => Math.Max(0, Math.Min(requested, victim.Health));

        /// <summary>Marks a victim whose Health a -Health enchantment took to 0 or less, for destruction in the next death phase.</summary>
        internal static void MarkIfDepleted(ICharacter victim)
        {
            if (victim.Health <= 0) victim.ToBeDestroyed = true;
        }
    }
}
