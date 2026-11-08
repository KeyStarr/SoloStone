using System.Linq;
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
    internal static class Consumption
    {
        internal const int DbfId = 127410;
        [StandardCard("CORE_CATA_007", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id);
                var targets = ctx.SampleDistinct(c.Opponent.BoardZone.GetAll()
                    .Where(m => !m.IsDead && !m.Untouchable).ToArray(), 2);
                ctx.DamageBatch(targets.Select(m => m.Id), 3, spellDamage: true);
                // D3: resolve both hits and their complete death phase before drawing for direct casualties.
                var killed = targets.Where(m => m.IsDead).Select(m => m.Id).ToArray();
                g.DeathProcessingAndAuraUpdate();
                int count = killed.Count(entity => ctx.History.Deaths().Any(f => f.EntityId == entity));
                for (int i = 0; i < count; i++)
                {
                    if (g.History) g.PowerHistory.Add(new SabberStoneCore.Kettle.PowerHistoryMetaData {
                        Type = MetaDataType.TARGET, Info = new System.Collections.Generic.List<int> { s.Id }
                    });
                    ctx.Draw();
                }
            }) };
        }
    }
}
