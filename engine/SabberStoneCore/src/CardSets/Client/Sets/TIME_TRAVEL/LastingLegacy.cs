using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LastingLegacy
    {
        internal const int DbfId = 121024;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(DeckHasNoMinions);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                ctx.GiveBuff(c.Hero.Id, "TIME_449e1");
                if (!DeckHasNoMinions((IPlayable)s)) return;
                foreach (var minion in c.HandZone.OfType<Minion>().ToArray()) ctx.GiveBuff(minion.Id, "TIME_449e2");
            }) };
        }

        internal static void ConfigureHeroBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(4)) { IsOneTurnEffect = true } };
        }

        internal static void ConfigureHandBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(4)) };
        }

        private static bool DeckHasNoMinions(IPlayable source) =>
            !source.Controller.DeckZone.Any(e => e.Id != source.Id && e.Card.Type == CardType.MINION);
    }
}
