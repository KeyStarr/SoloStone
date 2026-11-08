using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PetalPicker
    {
        internal const int DbfId = 115648;
        private static bool ImbuedTwice(IPlayable source) => ImbueRules.Count(source.Controller) >= 2;

        [StandardCard("FIR_921", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(ImbuedTwice);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (ImbueRules.Count(c) < 2) return;
                // The authored draw impact must branch on a typed source, before private drawn-card SHOWs.
                if (g.History) g.PowerHistory.Add(new PowerHistoryMetaData {
                    Type = MetaDataType.TARGET, Info = new List<int> { s.Id }
                });
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                ctx.Draw();
                ctx.Draw();
            }) };
        }
    }
}
