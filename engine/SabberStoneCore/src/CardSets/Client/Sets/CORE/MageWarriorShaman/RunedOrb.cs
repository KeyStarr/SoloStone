using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RunedOrb
    {
        internal const int DbfId = 126982;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new System.Collections.Generic.Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/CORE_BAR_541/v1",
                new DamageTask(2, EntityType.TARGET, spellDmg: true),
                new DiscoverThenContinueTask(CoreCostPools.Spell(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    foreach (var chosen in stack.Playables)
                    {
                        Generic.RemoveFromZone(c, chosen);
                        Generic.AddHandPhase(c, chosen);
                    }
                    stack.Playables.Clear();
                })) };
        }
    }
}
