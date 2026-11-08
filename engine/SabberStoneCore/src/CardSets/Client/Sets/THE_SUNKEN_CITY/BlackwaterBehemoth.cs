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
    internal static class BlackwaterBehemoth
    {
        [StandardCard("TSC_216", 72488)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.DeclareLayout(card, new[] { 1 }, "TSC_216t");
            card.Power = new Power();
        }

        [StandardCard("TSC_216t", 72489)]
        internal static void Token(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = new CustomTask((g,c,s,t,k) => {
                    var body = c.BoardZone.Where(m => m.Card.Id == "TSC_216" && !m.IsDead && !m.IsDormant).OrderBy(m => m.OrderOfPlay).FirstOrDefault();
                    var enemies = c.Opponent.BoardZone.Where(m => !m.IsDead && !m.Untouchable).ToArray();
                    if (body == null || enemies.Length == 0) return;
                    PastColossalCombat.Force(enemies[g.Random.Next(enemies.Length)], body);
                }) } };
        }
    }
}
