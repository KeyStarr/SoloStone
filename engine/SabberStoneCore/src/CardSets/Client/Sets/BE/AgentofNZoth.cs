using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AgentofNZoth
    {
        internal const int DbfId = 130761;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var candidates = c.HandZone.GetAll().Where(p => p is Minion && p.HasDeathrattle && p.Card.Id != s.Card.Id).ToList();
                if (candidates.Count == 0 || c.HandZone.IsFull) return;
                var selection = new TaskStack { Playables = candidates };
                new RandomTask(1, EntityType.STACK).Process(g, c, s, null, selection);
                new CopyTask(EntityType.STACK, Zone.HAND).Process(g, c, s, null, selection);
            }) };
        }
    }
}
