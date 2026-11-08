using System;
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
    internal static class TrickySatyr
    {
        internal const int DbfId = 114505;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var hand = c.Opponent.HandZone.ToArray(); if (hand.Length == 0) return;
                int lowest = hand.Min(card => card.Cost); var tied = hand.Where(card => card.Cost == lowest).ToArray();
                var chosen = tied[g.Random.Next(tied.Length)]; g.OnRandomHappened(true);
                Generic.Copy(c, s, chosen, Zone.HAND);
            }) };
        }
    }
}
