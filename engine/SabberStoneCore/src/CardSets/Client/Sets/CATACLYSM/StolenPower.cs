using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StolenPower
    {
        internal const int DbfId = 123074;
        [StandardCard("CATA_202", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                var chosen = new EffectContext(g,c.Id,s.Id).PickRandomCard(B14K6CataclysmPools.Shatter(), recipeDbfId: DbfId);
                if (chosen != null) ShatterRules.GiveCombined(c,chosen.Id,s);
            }) };
        }
    }
}
