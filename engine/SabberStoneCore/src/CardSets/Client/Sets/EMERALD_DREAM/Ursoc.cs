using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Ursoc
    {
        [StandardCard("EDR_819", 116018)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Tags[DeathContextRules.TrackCombatKills] = 1;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                int position = EffectContext.DeathrattlePosition(c, s);
                foreach (var entry in PayloadRules.Entries(s, DeathContextRules.KillKey))
                {
                    if (c.BoardZone.IsFull) break;
                    var body = (Minion)Entity.FromCard(c, entry.Card, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                    Generic.SummonBlock(g, body, position++, s);
                }
            }), PowerTask = new CustomTask((g, c, s, t, stack) => {
                var victims = c.BoardZone.Concat(c.Opponent.BoardZone).Where(m => m != s && !m.Untouchable).ToArray();
                for (int i = victims.Length - 1; i > 0; i--)
                { int j = g.Random.Next(i + 1); var swap = victims[i]; victims[i] = victims[j]; victims[j] = swap; }
                foreach (var victim in victims)
                {
                    if (!(s is Minion attacker) || attacker.Zone != c.BoardZone || attacker.ToBeDestroyed) break;
                    if (victim.Zone != victim.Controller.BoardZone || victim.ToBeDestroyed || victim.Untouchable || victim.IsImmune) continue;
                    new AttackTask(EntityType.SOURCE, EntityType.TARGET).Process(g, c, s, victim, stack);
                }
            }) };
        }
    }
}
