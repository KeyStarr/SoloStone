using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Paleomancy
    {
        internal const int DbfId = 117430;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CanKeepAll);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                if (!ctx.Discover(TheLostCityPools.UndeadDiscover(), recipeDbfId: DbfId) || !CanKeepAll((IPlayable)s) || !CorpseRules.TrySpend(c, 5)) return;
                var offers = c.Choice.Choices.ToArray();
                DiscoverRealRules.CompletedAll(c, s, offers.Select(id => g.IdEntityDic[id].Card).ToArray());
                // ChoicePick retires the prompt, but keep-all must not also record a single selection.
                c.Choice.HistoryDiscoverId = 0;
                foreach (int id in offers) DiscoverRealRules.Completed(c, g.IdEntityDic[id]);
                foreach (int id in offers) g.Facts.AdmitDefinition(g.IdEntityDic[id].Card);
                Generic.ChoicePick(c, g, offers[0]);
                foreach (int id in offers.Skip(1))
                {
                    var offer = g.IdEntityDic[id];
                    Generic.RemoveFromZone(c, offer); Generic.AddHandPhase(c, offer);
                }
            }) };
        }

        private static bool CanKeepAll(IPlayable source) =>
            source.Game.EnableCorpses && CorpseRules.Available(source.Controller) >= 5;
    }
}
