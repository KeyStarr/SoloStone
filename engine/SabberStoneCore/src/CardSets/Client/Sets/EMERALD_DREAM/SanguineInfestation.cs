using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SanguineInfestation
    {
        internal const int DbfId = 114966;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(new CustomTask((g, c, s, t, stack) => {
                // Authored draw impact must target the already revealed source, not a deck actor.
                if (g.History) g.PowerHistory.Add(new PowerHistoryMetaData {
                    Type = MetaDataType.TARGET, Info = new List<int> { s.Id }
                });
            }), EffectContext.Task(ctx => {
                ctx.Draw(); ctx.Draw();
                ctx.Summon("EDR_810t"); ctx.Summon("EDR_810t");
                return TaskState.COMPLETE;
            })) };
        }
    }
}
