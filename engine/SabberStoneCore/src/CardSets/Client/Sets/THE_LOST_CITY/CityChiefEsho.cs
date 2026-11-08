using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CityChiefEsho
    {
        internal const int DbfId = 121026;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(DeckMinionsShareRace);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!DeckMinionsShareRace((IPlayable)s)) return;
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                foreach (var minion in c.BoardZone.GetAll().Cast<IPlayable>().Concat(c.HandZone)
                    .Concat(c.DeckZone).OfType<Minion>().Where(p => p.Id != s.Id).ToArray())
                    ctx.GiveBuff(minion.Id, "TLC_110e");
            }) };
        }

        private static bool DeckMinionsShareRace(IPlayable source) =>
            Card.MinionsShareRace(source.Controller.DeckZone.Where(p => p.Id != source.Id).Select(p => p.Card));

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(2)) };
        }
    }
}
