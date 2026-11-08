using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EnvoyoftheGlade
    {
        internal const int DbfId = 116495;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                foreach (var original in c.DeckZone.Where(p => p.Card.Class == CardClass.NEUTRAL).ToArray())
                {
                    var chosen = ctx.PickRandomCard(EmeraldDreamPools.DruidCard(), recipeDbfId: DbfId);
                    if (chosen != null)
                        new TransformTask(chosen.Id, EntityType.TARGET, clearNativeKeywords: true).Process(g, c, s, original, stack);
                }
            }) };
        }
    }
}
