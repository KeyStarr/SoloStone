using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class VamaLoomingDeath
    {
        internal const int DbfId = 125462;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                foreach (var minion in c.BoardZone.GetAll().Concat(c.Opponent.BoardZone.GetAll()).Where(m => m.Card.Class != CardClass.PALADIN
                    && (m.Card[GameTag.MULTIPLE_CLASSES] & (1 << ((int)CardClass.PALADIN - 1))) == 0).ToArray())
                    ctx.Destroy(minion.Id);
            }) };
        }
    }
}
