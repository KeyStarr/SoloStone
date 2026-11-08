using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SupplyRun
    {
        internal const int DbfId = 121757;
        internal static void Configure(Card card)
        {
            ConfigureFamily(card);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                // An empty tutor returns STOP; the independent hand buff must still resolve.
                Draw().Process(g, c, s, t, stack);
                Buff().Process(g, c, s, t, stack);
            }) };
        }
        internal static void ConfigureFirst(Card card)
        {
            ConfigureFamily(card);
            card.Power = new Power { PowerTask = Draw() };
        }
        internal static void ConfigureSecond(Card card)
        {
            ConfigureFamily(card);
            card.Power = new Power { PowerTask = Buff() };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(2)) };
        }
        private static CustomTask Draw() => new CustomTask((g, c, s, t, stack) => {
            // Draw replacements can change the deck; each tutor must select from the current deck.
            for (int i = 0; i < 3; i++)
                new DrawMinionTask(Race.INVALID, 1, false).Process(g, c, s, t, stack);
        });
        private static CustomTask Buff() => new CustomTask((g, c, s, t, stack) => {
            foreach (var minion in c.HandZone.OfType<Minion>())
                Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_820e"), (IPlayable)s, minion);
        });
        private static void ConfigureFamily(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Shatter = new ShatterDefinition("CATA_820", "CATA_820t", "CATA_820t2");
        }
    }
}
