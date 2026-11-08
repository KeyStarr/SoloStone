using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StoryOfBarnabus
    {
        internal const int DbfId = 117648;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var drawStack = new TaskStack();
                new DrawMinionTask(Race.INVALID, 1, true).Process(g, c, s, null, drawStack);
                if (!(drawStack.Playables?.FirstOrDefault() is Minion drawn) || drawn.Zone != c.HandZone || drawn.AttackDamage < 5) return;
                Generic.AddEnchantmentBlock(g, Cards.FromId("TLC_231e"), (IPlayable)s, drawn);
                new ArmorTask(5).Process(g, c, s, null, stack);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Health_N(5)) };
        }
    }
}
