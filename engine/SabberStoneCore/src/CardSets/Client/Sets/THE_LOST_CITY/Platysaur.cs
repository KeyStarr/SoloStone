using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Platysaur
    {
        internal const int DbfId = 117719;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var drawn = Generic.Draw(c);
                if (drawn != null && drawn.Zone == c.HandZone)
                    Generic.AddEnchantmentBlock(g, Cards.FromId("TLC_603e"), (IPlayable)s, s, drawn.Id);
            }) };
        }

        internal static void ConfigureMemory(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                if (t is Enchantment memory && g.IdEntityDic.TryGetValue(memory.ScriptTag1, out var drawn) && drawn.Zone == c.HandZone)
                    new DiscardTask(EntityType.TARGET).Process(g, c, s, drawn);
            }) };
        }
    }
}
