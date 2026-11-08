using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class UnearthedArtifacts
    {
        internal const int DbfId = 118199;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.PoweredUpWhen(DiscoveredThisTurn);
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                bool discovered = DiscoveredThisTurn(ctx.History, ctx.ControllerId);
                var chosen = ctx.PickRandomCard(discovered ? TheLostCityPools.FourCostMinion() : TheLostCityPools.TwoCostMinion(), recipeDbfId: DbfId);
                if (chosen != null) ctx.Summon(chosen.Id);
                return TaskState.COMPLETE;
            }) };
        }

        private static bool DiscoveredThisTurn(IPlayable source) =>
            DiscoveredThisTurn(source.Game.HistoryFacts.Query, source.Controller.Id);
        private static bool DiscoveredThisTurn(HistoryQuery history, int controllerId) =>
            history.Discoveries(history.ThisTurn).Any(f => f.ControllerId == controllerId);
    }
}
