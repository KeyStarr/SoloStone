using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GaiaTheTechtonic
    {
        [StandardCard("TSC_029", 71841)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.Declare(card, "TSC_029t", "TSC_029t2");
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(m => m.Card.IsRace(Race.MECHANICAL)),
                SingleTask = new CustomTask((g,c,s,t,k) => {
                    var enemies = c.Opponent.BoardZone.Where(m => !m.Untouchable).Cast<ICharacter>().Append(c.Opponent.Hero).ToArray();
                    using var wave = g.History ? g.PowerHistory.DamageWave(enemies.Length) : null;
                    foreach (var enemy in enemies)
                        enemy.TakeDamage((IPlayable)s, 1);
                }) } };
        }

        [StandardCard("TSC_029t", 80793)]
        [StandardCard("TSC_029t2", 85858)]
        internal static void Token(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}
