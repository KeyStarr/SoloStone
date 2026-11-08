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
    internal static class Sawbones
    {
        internal const int DbfId = 126260;
        [StandardCard("JAIL_444", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id);
                var victims = c.BoardZone.GetAll().Where(m => m.Id != s.Id && !m.Untouchable && !m.IsDead).ToArray();
                foreach (var minion in victims) minion.Destroy();
                g.DeathProcessingAndAuraUpdate();
                int count = victims.Count(m => ctx.History.Deaths().Any(f => f.EntityId == m.Id));
                for (int i = 0; i < count; i++)
                {
                    // Authored draw CardDefs need a typed source impact before each draw SHOW.
                    if (g.History) g.PowerHistory.Add(new SabberStoneCore.Kettle.PowerHistoryMetaData {
                        Type = MetaDataType.TARGET, Info = new System.Collections.Generic.List<int> { s.Id }
                    });
                    ctx.Draw(); ctx.RefreshMana(1);
                }
            }) };
        }
    }
}
