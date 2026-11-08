using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Collections.Generic;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HighCultistHerenn
    {
        internal const int DbfId = 117231;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var recruited = new List<Minion>();
                for (int i = 0; i < 2 && !c.BoardZone.IsFull; i++)
                {
                    var candidates = c.DeckZone.OfType<Minion>().Where(m => m.HasDeathrattle).ToArray();
                    if (candidates.Length == 0) break;
                    var selected = candidates[g.Random.Next(candidates.Length)]; g.OnRandomHappened(true);
                    c.DeckZone.Remove(selected);
                    int position = s.Zone == c.BoardZone ? ((IPlayable)s).ZonePosition + 1
                        + recruited.Count(m => m.Zone == c.BoardZone) : -1;
                    Generic.SummonBlock(g, selected, position, s);
                    recruited.Add(selected);
                    g.DeathProcessingAndAuraUpdate();
                }
                if (recruited.Count != 2 || recruited.Any(m => m.IsDead || m.Zone != c.BoardZone || m.Untouchable)) return;
                new AttackTask(EntityType.STACK, EntityType.TARGET).Process(g, c, s, recruited[1],
                    new TaskStack { Playables = new[] { recruited[0] } });
            }) };
        }
    }
}
