using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Moragg
    {
        internal const int DbfId = 124073;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                if (c.BoardZone.IsFull) return;
                var pool = c.DeckZone.GetAll().OfType<Minion>().Where(m => m.IsRace(Race.DEMON)).ToArray();
                if (pool.Length == 0) return;
                var recruited = pool[pool.Length == 1 ? 0 : g.Random.Next(pool.Length)];
                if (pool.Length > 1) g.OnRandomHappened(true);
                int position = EffectContext.DeathrattlePosition(c, s);
                c.DeckZone.Remove(recruited);
                Generic.SummonBlock(g, recruited, position, s);
                if (recruited.Zone == c.BoardZone)
                    Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_906e"), (IPlayable)s, recruited);
            }) };
        }

        internal static void ConfigureDeathrattle(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                if (c.BoardZone.IsFull) return;
                int position = EffectContext.DeathrattlePosition(c, s);
                Generic.SummonBlock(g, (Minion)Entity.FromCard(c, Cards.FromId("JAIL_906")), position, s);
            }) };
        }
    }
}
