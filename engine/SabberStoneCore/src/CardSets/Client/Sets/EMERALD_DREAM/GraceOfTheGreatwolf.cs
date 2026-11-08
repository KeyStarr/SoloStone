using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GraceOfTheGreatwolf
    {
        [StandardCard("EDR_263", 115058)]
        internal static void Configure(Card card)
        { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power(); }
        [StandardCard("EDR_263a", 115497)]
        internal static void ConfigureDamage(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DamageTask(4, EntityType.OP_HERO, true) };
        }
        [StandardCard("EDR_263b", 115498)]
        internal static void ConfigureWolves(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new SummonTask("EDR_850pe",2) };
        }
    }
}
