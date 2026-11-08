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
    internal static class Ozumat
    {
        [StandardCard("TID_711", 75580)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.DeclareLayout(card, new[] { 0, 0, 0, 1, 2, 3 }, "TID_711t", "TID_711t2", "TID_711t3", "TID_711t4", "TID_711t5", "TID_711t6");
            card.Power = new Power { DeathrattleTask = new CustomTask((g,c,s,t,k) => {
                int count = c.BoardZone.Count(m => m.Card.Id.StartsWith("TID_711t", StringComparison.Ordinal) && !m.IsDead && !m.IsDormant);
                var enemies = c.Opponent.BoardZone.Where(m => !m.IsDead && !m.Untouchable).ToList();
                for (int i = 0; i < count && enemies.Count > 0; i++) {
                    int index = g.Random.Next(enemies.Count); enemies[index].Destroy(); enemies.RemoveAt(index);
                }
            }) };
        }

        [StandardCard("TID_711t", 75581)]
        [StandardCard("TID_711t2", 85862)]
        [StandardCard("TID_711t3", 85863)]
        [StandardCard("TID_711t4", 85864)]
        [StandardCard("TID_711t5", 85865)]
        [StandardCard("TID_711t6", 85866)]
        internal static void Token(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}
