using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Hematurge
    {
        internal const int DbfId = 111320;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CanSpendCorpse);
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CORE_RLK_066/v1",
                new CustomTask((g, c, s, t, stack) => stack.Flag = CorpseRules.TrySpend(c, 1)),
                new FlagTask(false, EffectContext.Task(ctx => TaskState.STOP)),
                new DiscoverThenContinueTask(OfflinePoolDeclarations.BloodRuneCard(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen != null && Generic.RemoveFromZone(c, chosen))
                        Generic.AddHandPhase(c, chosen);
                })) };
        }
        private static bool CanSpendCorpse(IPlayable source) =>
            source.Game.EnableCorpses && CorpseRules.Available(source.Controller) >= 1;

    }
}
