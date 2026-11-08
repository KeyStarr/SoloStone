using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PossessedAnimancer
    {
        internal const int DbfId = 118494;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                if (c.BoardZone.IsFull) return;
                var beasts = c.DeckZone.Where(p => p is Minion m && m.IsRace(Race.BEAST)).ToArray();
                if (beasts.Length == 0) return;
                var ctx = new EffectContext(g, c.Id, s.Id);
                var chosen = ctx.SampleDistinct(beasts, 1)[0];
                int position = EffectContext.DeathrattlePosition(c, s);
                var summon = new TaskStack { Playables = new System.Collections.Generic.List<IPlayable> { chosen }, Number = position + 1 };
                Generic.RemoveFromZone(c, chosen);
                new SummonTask(SummonSide.NUMBER).Process(g, c, s, null, summon);
                if (chosen.Zone == c.BoardZone) ComplexTask.LifeSteal(EntityType.TARGET).Process(g, c, s, chosen);
            }) };
        }
    }
}
