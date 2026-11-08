using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DreadsoulCorrupter
    {
        internal const int DbfId = 114663;
        internal static void Configure(Card card)
        {
            Dreadseeds.ConfigureGenerator(card);
            card.Power = new Power { PowerTask = Dreadseeds.Summon(SummonSide.RIGHT),
                DeathrattleTask = Dreadseeds.Summon(SummonSide.DEATHRATTLE) };
        }
    }
}
