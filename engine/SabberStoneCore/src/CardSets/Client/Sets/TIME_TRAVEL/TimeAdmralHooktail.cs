using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TimeAdmralHooktail
    {
        internal const int DbfId = 120805;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new SummonOpTask("TIME_713t") };
        }

        internal static void ConfigureChest(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                int count = c.Opponent.HandZone.FreeSpace;
                if (count > 0) new AddCardTo("GAME_005", EntityType.OP_HAND, count).Process(g, c, s, t, stack);
            }) };
        }
    }
}
