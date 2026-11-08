using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EmeraldBounty
    {
        [StandardCard("EDR_234", 114281)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                for (int i = 0; i < 2; i++)
                    PlayLockRules.LockHand(Generic.Draw(c), (IPlayable)s, "EDR_234e2", 2);
            }) };
        }

        [StandardCard("EDR_234e2", 116401)]
        internal static void ConfigureLock(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = PlayLockRules.Expiry() };
        }
    }
}
