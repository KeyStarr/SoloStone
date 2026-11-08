using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SupremeDinomancy
    {
        internal const int DbfId = 117602;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var minion in c.BoardZone.GetAll().Cast<IPlayable>().Concat(c.HandZone).Concat(c.DeckZone).OfType<Minion>().Where(m => m.IsRace(Race.BEAST)).ToArray())
                    Generic.AddEnchantmentBlock(g, Cards.FromId("TLC_828e"), (IPlayable)s, minion);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(2)) };
        }
    }
}
