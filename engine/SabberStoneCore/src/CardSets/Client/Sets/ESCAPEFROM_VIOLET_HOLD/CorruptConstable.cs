using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CorruptConstable
    {
        internal const int DbfId = 127037;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HasEnemyImp);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var imps = EnemyImps((IPlayable)s).ToArray();
                if (imps.Length == 0) return;
                var chosen = imps[g.Random.Next(imps.Length)];
                c.Opponent.DeckZone.PutOnTop(chosen);
                new EffectContext(g, c.Id, s.Id, stack: stack).GiveBuff(chosen.Id, "CAP_401e");
            }) };
        }

        private static IEnumerable<IPlayable> EnemyImps(IPlayable source) => source.Controller.Opponent.DeckZone
            .Where(held => held.Card.Id == "CAP_400t2t");
        private static bool HasEnemyImp(IPlayable source) => EnemyImps(source).Any();

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(2)) };
        }
    }
}
