using System;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FelwoodTreant
    {
        internal const int DbfId = 122967;
        [StandardCard("CATA_131", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Remaining);
            card.PoweredUpWhen(Ready);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (Ready((IPlayable)s)) Generic.ChangeManaCrystal(c, 1, true);
                else Generic.AddTempMana(c, 1);
            }) };
        }

        private static int Remaining(IPlayable source) =>
            Math.Max(0, 4 - source.Game.HistoryFacts.Query.ManaSpentWhileHolding(source.Id, source.Controller.Id));
        private static bool Ready(IPlayable source) => Remaining(source) == 0;
    }
}
