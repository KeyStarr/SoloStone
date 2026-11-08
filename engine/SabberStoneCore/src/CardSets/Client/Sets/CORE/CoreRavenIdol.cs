using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreRavenIdol
    {
        [StandardCard("Core_LOE_115", 113109)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
        }
        [StandardCard("LOE_115a", 38112)]
        internal static void ConfigureMinion(Card card)
        {
            card.Implemented = true;
            var legacy = card.Power;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (s.Card.Id == "Core_LOE_115") CoreIdentityPools.Discover(g, c, s, 113109, CoreIdentityPools.Minion());
                else legacy.PowerTask.Process(g, c, s, t, stack);
            }) };
        }
        [StandardCard("LOE_115b", 38113)]
        internal static void ConfigureSpell(Card card)
        {
            card.Implemented = true;
            var legacy = card.Power;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (s.Card.Id == "Core_LOE_115") CoreIdentityPools.Discover(g, c, s, 113109, CoreIdentityPools.Spell());
                else legacy.PowerTask.Process(g, c, s, t, stack);
            }) };
        }
    }
}
