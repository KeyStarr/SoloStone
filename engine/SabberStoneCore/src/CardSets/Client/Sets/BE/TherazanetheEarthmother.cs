using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TherazanetheEarthmother
    {
        internal const int DbfId = 131010;
        internal static void Configure(Card card)
        {
            card.Implemented=true;
            card.ExcludeFromLegacyRandomPools=true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,k) =>
                RepeatRules.Grant(c,(IPlayable)s,"BE_337e2",schools:254)) };
        }
    }
}
