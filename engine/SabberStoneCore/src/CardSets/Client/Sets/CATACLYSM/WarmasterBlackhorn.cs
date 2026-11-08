using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WarmasterBlackhorn
    {
        internal const int DbfId = 122635;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var owner in new[] { c, c.Opponent })
                    foreach (var card in owner.DeckZone.GetAll().Where(p => p.Cost <= 2))
                    {
                        if (Actions.DrawReplacementRules.DestroyOffboard(card)) continue;
                        owner.DeckZone.Remove(card);
                        owner.GraveyardZone.Add(card);
                    }
            }) };
        }

    }
}
