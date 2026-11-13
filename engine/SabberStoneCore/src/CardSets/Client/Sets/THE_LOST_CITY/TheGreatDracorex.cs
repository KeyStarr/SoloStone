using System.Linq;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TheGreatDracorex
    {
        [StandardCard("DINO_401", 118404)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.SELF,
                Condition = new SelfCondition(m => m.Game.CurrentEventData?.EventTarget is Minion defender && defender.Controller != m.Controller),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    int defender = g.CurrentEventData.EventTarget.Id;
                    g.DeathProcessingAndAuraUpdate();
                    int attack = s.Zone == c.BoardZone ? ((Minion)s).AttackDamage : s.Card.ATK;
                    var victims = c.Opponent.BoardZone.Where(m => m.Id != defender && !m.Untouchable).ToArray();
                    using var wave = g.History ? g.PowerHistory.DamageWave(victims.Length) : null;
                    foreach (var victim in victims)
                        victim.TakeDamage((IPlayable)s, attack);
                })
            } };
        }
    }
}
