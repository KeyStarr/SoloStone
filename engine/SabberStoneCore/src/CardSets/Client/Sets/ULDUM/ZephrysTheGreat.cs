using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Tasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    /// <summary>
    /// Zephrys the Great (ULD_003): "Battlecry: If your deck has no duplicates, wish for the perfect card." The deck is read
    /// when he is played, by card id; an empty deck has no duplicates. The wish is a private choice of up to three cards
    /// (<see cref="ZephrysWish"/>) and is not a Discover. Glows while the deck has no duplicates.
    /// </summary>
    internal static class ZephrysTheGreat
    {
        internal const int DbfId = 53756;

        /// <summary>Registered by <see cref="WildModules"/>: a Wild card, not part of the Standard corpus.</summary>
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/ULD_003/v1",
                new ConditionTask(EntityType.SOURCE, SelfCondition.IsNoDupeInDeck),
                new FlagTask(false, EffectContext.Task(ctx => TaskState.STOP)),
                new DiscoverThenContinueTask((g, c, s, k) => ZephrysWish.Offers(g, c, s)),
                new CustomTask((g, c, s, t, k) => {
                    var chosen = k.Playables.FirstOrDefault();
                    if (chosen != null) Generic.AddHandPhase(c, c.SetasideZone.Remove(chosen));
                })) };
            card.PoweredUpWhen(NoDuplicates);
        }

        private static bool NoDuplicates(Model.Entities.IPlayable source) => SelfCondition.IsNoDupeInDeck.Eval(source);
    }
}
