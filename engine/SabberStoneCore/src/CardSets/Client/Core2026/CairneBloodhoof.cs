using SabberStoneCore.Enchants;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Core2026
{
    internal static class CairneBloodhoof
    {
        internal const int DbfId = 69667;
        internal static void Configure(Card card)
        {
            var baine = ClientOverlay.Snapshot("EX1_110t");
            baine.Implemented = true;
            card.Power = new Power { DeathrattleTask = new SummonTask(SummonSide.DEATHRATTLE, baine) };
        }
    }
}
