using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class P1CKP0K3T
    {
        internal const int DbfId = 127322;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(LargeDeck);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (LargeDeck((IPlayable)s)) new EffectContext(g, c.Id, s.Id, stack: stack).Draw();
            }) };
        }
        private static bool LargeDeck(IPlayable source) =>
            source.Controller.DeckZone.Count >= 25;

    }
}
