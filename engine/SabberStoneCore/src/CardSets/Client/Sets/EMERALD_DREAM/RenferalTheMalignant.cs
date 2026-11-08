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
    internal static class RenferalTheMalignant
    {
        private static int Plays(IPlayable source) => source.Game.HistoryFacts.Query.Plays()
            .Count(f => f.ControllerId == source.Controller.Id && f.DefinitionKnown && f.DbfId == 114518);
        private static int Display(IPlayable source) => Math.Max(1, Plays(source) + (source.Zone == source.Controller.HandZone ? 1 : 0));

        [StandardCard("EDR_526", 114518)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Display);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var candidates = c.Opponent.HandZone.Where(p => p[GameTag.CANT_PLAY] == 0).ToList();
                int count = Math.Min(Math.Max(1, Plays((IPlayable)s)), candidates.Count);
                for (int i = 0; i < count; i++)
                {
                    int index = g.Random.Next(candidates.Count);
                    PlayLockRules.LockHand(candidates[index], (IPlayable)s, "EDR_526e");
                    candidates.RemoveAt(index);
                }
            }) };
        }

        [StandardCard("EDR_526e", 120480)]
        internal static void ConfigureLock(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = PlayLockRules.Expiry() };
        }
    }
}
