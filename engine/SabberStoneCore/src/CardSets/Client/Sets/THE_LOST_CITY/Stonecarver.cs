using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Stonecarver
    {
        internal const int DbfId = 117918;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var eligible = c.BoardZone.GetAll().Where(m => m.Id != s.Id && m.Damage > 0).ToArray();
                    if (eligible.Length == 0) return;
                    g.OnRandomHappened(true);
                    new EffectContext(g, c.Id, s.Id, stack: stack).GiveBuff(eligible[g.Random.Next(eligible.Length)].Id, "TLC_623e");
                })
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(2)) };
        }
    }
}
