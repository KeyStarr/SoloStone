using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CosmicSprings
    {
        internal const int DbfId = 129787;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { LocationActivation = new LocationActivation {
                Task = EffectContext.Task(context => {
                    var drawn = context.DrawEntity();
                    if (drawn != null && drawn.Card.Type != CardType.MINION) context.RefreshMana(2);
                    return context.RunLegacy(new SetGameTagTask(GameTag.LOCATION_ACTION_COOLDOWN, 1, EntityType.SOURCE));
                })
            } };
        }
    }
}
