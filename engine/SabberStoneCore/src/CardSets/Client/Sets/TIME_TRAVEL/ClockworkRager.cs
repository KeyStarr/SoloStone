using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ClockworkRager
    {
        internal const int DbfId = 120860;
        private static int Turns(IPlayable source) => source.Game.HistoryFacts.Query.TurnsTaken(source.Controller.Id);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            // Rule: count own begun turns, including current and extra turns (design R3).
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Turns);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int turns = g.HistoryFacts.Query.TurnsTaken(c.Id);
                if (turns > 0) Generic.AddEnchantmentBlock(g, Cards.FromId("TIME_048e"), (IPlayable)s, (IPlayable)s, turns);
            }) };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Health_N(0)) { UseScriptTag = true } };
        }
    }
}
