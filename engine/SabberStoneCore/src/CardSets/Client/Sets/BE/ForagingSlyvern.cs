using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using System;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ForagingSlyvern
    {
        internal const int DbfId = 134936;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                int count = Math.Max(0, ((Minion)s).AttackDamage);
                for (int i = 0; i < count; i++) Generic.Draw(c);
            }) };
        }

    }
}
