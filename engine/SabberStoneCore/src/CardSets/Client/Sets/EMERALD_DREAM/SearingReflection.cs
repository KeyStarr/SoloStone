using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SearingReflection
    {
        internal const int DbfId = 115776;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var drawn = new TaskStack();
                new DrawMinionTask(Race.INVALID, 1, true).Process(g, c, s, t, drawn);
                if (drawn.Playables?.FirstOrDefault() is not Minion minion || c.BoardZone.IsFull) return;
                Generic.Copy(c, s, minion, Zone.PLAY, initialize: copy => {
                    Generic.AddEnchantmentBlock(g, Cards.FromId("FIR_941e1"), (IPlayable)s, copy);
                    ((Minion)copy).HasDivineShield = true;
                });
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(8), Effects.SetMaxHealth(8)) };
        }
    }
}
