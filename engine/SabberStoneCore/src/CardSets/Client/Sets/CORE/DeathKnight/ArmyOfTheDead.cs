using System;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ArmyOfTheDead
    {
        internal const int DbfId = 78352;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CanRaise);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                // Unpinned raise-fits-board: spend only for available physical slots.
                int count = CorpseRules.SpendUpTo(c, Math.Min(5, c.BoardZone.FreeSpace));
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                for (int i = 0; i < count; i++) ctx.Summon("RLK_008t");
            }) };
        }
        private static bool CanRaise(IPlayable source) => source.Game.EnableCorpses
            && CorpseRules.Available(source.Controller) > 0 && source.Controller.BoardZone.FreeSpace >= 1;
        internal static void ConfigureToken(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
        }
    }
}
