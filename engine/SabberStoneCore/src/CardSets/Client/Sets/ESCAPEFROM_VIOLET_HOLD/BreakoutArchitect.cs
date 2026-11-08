using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BreakoutArchitect
    {
        internal const int DbfId = 126354;
        internal static void Configure(Card card)
        {
            card.Implemented=true;
            card.ExcludeFromLegacyRandomPools=true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/JAIL_123/v1",
                new DiscoverThenContinueTask(EscapeFromVioletHoldPools.ArchitectSpell(),recipeDbfId:DbfId),
                new CustomTask((g,c,s,t,k) => {
                    var selected=k.Playables.FirstOrDefault();
                    if (selected == null) return;
                    RepeatRules.Mark(selected,(IPlayable)s,"JAIL_123e");
                    Generic.AddHandPhase(c,selected);
                })) };
        }
    }
}
