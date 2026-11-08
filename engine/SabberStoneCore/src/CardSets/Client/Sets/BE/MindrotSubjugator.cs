using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MindrotSubjugator
    {
        internal const int DbfId = 129808;
        [StandardCard("BE_093", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (!(s is Minion source) || source.Zone != c.BoardZone) return;
                var neighbors = source.GetAdjacentMinions();
                foreach (var neighbor in neighbors)
                    if (source.Zone == c.BoardZone && neighbor.Zone == c.BoardZone)
                        new TransformCopyTask().Process(g,c,neighbor,source);
            }) };
        }
    }
}
