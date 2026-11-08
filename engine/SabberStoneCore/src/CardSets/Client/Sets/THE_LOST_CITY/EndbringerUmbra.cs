using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EndbringerUmbra
    {
        [StandardCard("TLC_106", 117877)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                var ctx = new EffectContext(g,c.Id,s.Id,stack:stack);
                var tickets = ctx.History.Deaths().Where(f => f.ControllerId == c.Id
                    && f.Definition.ReadDefinition()[GameTag.DEATHRATTLE] > 0).ToList();
                for (int i = 0; i < 5 && i < tickets.Count; i++)
                {
                    int j = i + g.Random.Next(tickets.Count-i);
                    var swap=tickets[i]; tickets[i]=tickets[j]; tickets[j]=swap;
                }
                var selected = tickets.Take(5).ToArray();
                if (selected.Length > 0) g.OnRandomHappened(true);
                foreach (var death in selected)
                {
                    if (g.State == State.COMPLETE) break;
                    var source = (Minion)Entity.FromCard(c,death.Definition.Materialize(),zone:c.SetasideZone);
                    source[GameTag.DISPLAYED_CREATOR] = s.Id;
                    source.LastBoardPosition = c.BoardZone.SlotCount;
                    g.TaskQueue.StartEvent();
                    try
                    {
                        new ActivateDeathrattleTask(EntityType.TARGET).Process(g,c,s,source,stack);
                        g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
                    }
                    finally { g.TaskQueue.EndEvent(); }
                }
            }) };
        }
    }
}
