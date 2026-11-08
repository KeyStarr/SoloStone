using SabberStoneCore.Actions;
using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BloodpetalBiome
    {
        internal const int DbfId = 117837;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                Task = StateTaskList.Suspendable("client/TLC_449/v1",
                    new SetGameTagTask(GameTag.LOCATION_ACTION_COOLDOWN, 1, EntityType.SOURCE),
                    new DiscoverThenContinueTask(TheLostCityPools.Biome()),
                    new CustomTask((g, c, s, t, stack) => {
                        var chosen = stack.Playables.SingleOrDefault();
                        if (chosen == null) return;
                        Generic.RemoveFromZone(c, chosen);
                        TemporaryRules.Make(chosen);
                        Generic.AddHandPhase(c, chosen);
                    }))
            } };
        }
    }
}
