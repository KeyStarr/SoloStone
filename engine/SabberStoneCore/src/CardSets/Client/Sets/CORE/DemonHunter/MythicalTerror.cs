using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MythicalTerror
    {
        internal const int DbfId = 129169;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) { SingleTask = ForceAll() } };
        }
        internal static CustomTask ForceAll() => new CustomTask((g, c, s, t, stack) => {
            if (!(s is Minion defender)) return;
            var attackers = c.Opponent.BoardZone.Where(m => !m.IsDead && !m.Untouchable).OrderBy(m => m.OrderOfPlay).ToArray();
            foreach (var attacker in attackers)
            {
                if (defender.IsDead || defender.Zone != c.BoardZone || defender.Untouchable) break;
                if (attacker.IsDead || attacker.Zone != c.Opponent.BoardZone || attacker.Untouchable) continue;
                new AttackTask(EntityType.TARGET, EntityType.SOURCE).Process(g, c, s, attacker);
            }
        });
    }
}
