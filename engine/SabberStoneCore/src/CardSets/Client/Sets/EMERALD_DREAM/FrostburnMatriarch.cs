using System;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FrostburnMatriarch
    {
        internal const int DbfId = 115625;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(EmeraldDreamConditions.HoldingDarkGift);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!EmeraldDreamConditions.HoldingDarkGift((IPlayable)s)) return;
                var board = c.BoardZone;
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                for (int i = 0; i < 2 && !board.IsFull; i++)
                    ctx.Summon("FIR_901t", s.Zone == board ?
                        Math.Min(((IPlayable)s).ZonePosition + 1 + i, board.SlotCount) : board.SlotCount);
            }) };
        }
    }
}
