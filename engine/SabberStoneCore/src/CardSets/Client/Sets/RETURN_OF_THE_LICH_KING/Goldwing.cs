using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Goldwing
    {
        private static bool HoldingMech(IPlayable source) => source.Controller.HandZone.Any(c => c.Id != source.Id && c.Card.IsRace(Race.MECHANICAL));
        [StandardCard("NX2_022", 86660)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(HoldingMech);
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (HoldingMech((IPlayable)s)) Effects.Windfury.ApplyTo((IPlayable)s);
            }) };
        }
    }
}
