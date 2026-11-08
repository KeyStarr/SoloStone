using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using System;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DarkscaleBroodmother
    {
        internal const int DbfId = 122500;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CataclysmHighlightConditions.HoldingAnotherDragon);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (CataclysmHighlightConditions.HoldingAnotherDragon((IPlayable)s))
                    new SetControllerGameTagTask(GameTag.RESOURCES_USED, Math.Max(0, c.UsedMana - 2)).Process(g, c, s, t, stack);
            }) };
        }

    }
}
