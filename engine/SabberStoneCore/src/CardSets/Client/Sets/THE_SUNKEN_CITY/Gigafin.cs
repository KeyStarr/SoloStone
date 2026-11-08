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
    internal static class Gigafin
    {
        [StandardCard("TSC_962", 74741)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            ColossalRules.DeclareLayout(card, new[] { 1 }, "TSC_962t");
            card.Power = new Power {
                PowerTask = new CustomTask((g,c,s,t,k) => {
                    var victims = c.Opponent.BoardZone.Where(m => !m.IsDead && !m.Untouchable).ToArray();
                    foreach (var victim in victims) PayloadRules.StoreCard(s, Food, victim.Card);
                    foreach (var victim in victims) victim.Destroy();
                }),
                DeathrattleTask = new CustomTask((g,c,s,t,k) => {
                    foreach (var food in PayloadRules.Entries(s, Food)) {
                        if (c.Opponent.BoardZone.IsFull) break;
                        var copy = (Minion)Entity.FromCard(c.Opponent, food.Card);
                        Generic.SummonBlock(g, copy, -1, s);
                    }
                }) };
        }

        [StandardCard("TSC_962t", 74742)]
        internal static void Token(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g,c,s,t,k) => {
                foreach (var body in c.BoardZone.Where(m => m.Card.Id == "TSC_962").ToArray())
                    foreach (var food in PayloadRules.Entries(body, Food)) PayloadRules.Remove(food);
            }) };
        }

        [StandardCard("TSC_962e", 74743)]
        [StandardCard("TSC_962e2", 79114)]
        internal static void Annotation(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
        internal const int Food = 74741;
    }
}
