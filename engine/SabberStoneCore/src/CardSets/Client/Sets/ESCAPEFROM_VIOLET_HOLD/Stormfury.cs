using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Actions;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Stormfury
    {
        internal const int DbfId = 126413;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.HandProgress = new HandProgressDefinition("JAIL_805t", 3, HandProgressMode.OwnerSpells);
            card.Power = new Power { PowerTask = new DamageTask(2, EntityType.OP_MINIONS, true) };
        }
        internal static void ConfigureElemental(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DamageTask(2, EntityType.OP_MINIONS) };
        }
    }
}
