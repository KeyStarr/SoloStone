using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Merithra
    {
        internal const int DbfId = 114284;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (c.BoardZone.IsFull) return;
                // DiamondSpellstone uses distinct printed identities from destroyed graveyard entities.
                var pool = c.GraveyardZone.OfType<Minion>().Where(m => m.ToBeDestroyed && m.Card.Cost >= 8 && m.Card.Id != s.Card.Id)
                    .Select(m => m.Card.Id).Distinct().ToArray();
                if (pool.Length == 0) return;
                var order = pool.OrderBy(id => g.Random.Next()).ToArray();
                if (pool.Length > 1) g.OnRandomHappened(true);
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int position = s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount;
                for (int i = 0; i < order.Length && !c.BoardZone.IsFull; i++) ctx.Summon(order[i], position + i);
            }) };
        }
    }
}
