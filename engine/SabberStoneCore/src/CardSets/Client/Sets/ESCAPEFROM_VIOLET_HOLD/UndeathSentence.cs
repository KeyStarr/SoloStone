using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class UndeathSentence
    {
        internal const int DbfId = 126867;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var deaths = ctx.History.Deaths().Where(f => f.ControllerId == c.Id
                    && f.Definition.ReadDefinition()[GameTag.DEATHRATTLE] > 0).ToArray();
                if (deaths.Length == 0) return;
                var chosen = ctx.SampleDistinct(deaths, 1)[0];
                var source = (Minion)Entity.FromCard(c, chosen.Definition.Materialize(), zone: c.SetasideZone);
                source[GameTag.DISPLAYED_CREATOR] = s.Id;
                source.LastBoardPosition = c.BoardZone.SlotCount;
                new ActivateDeathrattleTask(EntityType.TARGET).Process(g, c, s, source, stack);
            }) };
        }
    }
}
