using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PetalPeddler
    {
        internal const int DbfId = 115191;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var dragons = c.BoardZone.Where(m => m.Id != s.Id && !m.Untouchable && !m.IsDead && m.IsRace(Race.DRAGON)).ToArray();
                    if (dragons.Length == 0) return;
                    var chosen = dragons[g.Random.Next(dragons.Length)];
                    g.OnRandomHappened(true);
                    Generic.AddEnchantmentBlock(g, Cards.FromId("EDR_889e"), (IPlayable)s, chosen);
                })
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) };
        }
    }
}
