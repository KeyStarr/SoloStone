using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HijackedSecuritybot
    {
        internal const int DbfId = 127688;
        // Printed Prepare (tag 4354) is the PrepareRules hand action; the Battlecry buffs the other friendly minions.
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                foreach (var minion in c.BoardZone.GetAll().Where(m => m != s && !m.Untouchable).ToArray())
                    Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_457e"), (IPlayable)s, minion);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1), Effects.Health_N(1)) };
        }
    }
}
