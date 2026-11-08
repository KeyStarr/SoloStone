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
    internal static class BriarspawnDrake
    {
        [StandardCard("EDR_453", 114213)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var targets = c.Opponent.BoardZone.Where(m => !m.Untouchable && !m.IsImmune && !m.ToBeDestroyed).ToArray();
                    if (targets.Length == 0 || !DeathContextRules.Active((Minion)s)) return;
                    var victim = targets[g.Random.Next(targets.Length)];
                    int previous = s[DeathContextRules.ExcessCombat];
                    s[DeathContextRules.ExcessCombat] = 1;
                    try { new AttackTask(EntityType.SOURCE, EntityType.TARGET).Process(g, c, s, victim, stack); }
                    finally { if (previous == 0) s.NativeTags.Remove(DeathContextRules.ExcessCombat); else s[DeathContextRules.ExcessCombat] = previous; }
                })
            } };
        }
    }
}
