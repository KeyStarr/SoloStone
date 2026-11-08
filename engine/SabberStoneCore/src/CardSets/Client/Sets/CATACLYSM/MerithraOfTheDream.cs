using System;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MerithraOfTheDream
    {
        internal const int DbfId = 122978;
        [StandardCard("CATA_140", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Remaining);
            card.PoweredUpWhen(Ready);
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                bool ready = Ready((IPlayable)s);
                var ctx = new EffectContext(g,c.Id,s.Id,stack:stack);
                int slots = c.HandZone.FreeSpace;
                for (int i=0;i<slots;i++)
                {
                    var chosen = ctx.PickRandomCard(B14H4CataclysmPools.Dragon(),recipeDbfId:DbfId);
                    if (chosen == null) continue;
                    var dragon = Generic.DrawCard(c,chosen);
                    if (ready && dragon.Zone==c.HandZone)
                        dragon.Cost = 1;
                }
            }) };
        }
        private static int Remaining(IPlayable s) => Math.Max(0,25-s.Game.HistoryFacts.Query.ManaSpentWhileHolding(s.Id,s.Controller.Id));
        private static bool Ready(IPlayable s) => s.Game.HistoryFacts.Query.ManaSpentWhileHolding(s.Id,s.Controller.Id)>=25;
    }
}
