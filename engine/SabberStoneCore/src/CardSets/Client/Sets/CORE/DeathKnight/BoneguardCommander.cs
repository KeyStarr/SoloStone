using System;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BoneguardCommander
    {
        internal const int DbfId = 128334;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CanRaise);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                // Unpinned raise-fits-board: spend only for available physical slots.
                int count = CorpseRules.SpendUpTo(c, Math.Min(6, c.BoardZone.FreeSpace));
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                for (int i = 0; i < count; i++) ctx.Summon("RLK_061t");
            }) };
        }
        private static bool CanRaise(IPlayable source) => source.Game.EnableCorpses
            && CorpseRules.Available(source.Controller) > 0 && source.Controller.BoardZone.FreeSpace >= 2;
        internal static void ConfigureToken(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
        }
    }
}
