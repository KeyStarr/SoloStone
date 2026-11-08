using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TruthSeeker
    {
        internal const int DbfId = 126619;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.HERO,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    foreach (var minion in c.BoardZone.GetAll().Where(m => m.Card.Class == CardClass.PALADIN
                        || (m.Card[GameTag.MULTIPLE_CLASSES] & (1 << ((int)CardClass.PALADIN - 1))) != 0).ToArray())
                        Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_329e"), (IPlayable)s, minion);
                })
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2), Effects.Health_N(2)) };
        }
    }
}
