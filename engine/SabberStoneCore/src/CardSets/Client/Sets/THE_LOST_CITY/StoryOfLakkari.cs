using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StoryOfLakkari
    {
        [StandardCard("TLC_466", 118243)]
        internal static void Configure(Card card) => ObjectiveRules.Declare(card, 3,
            new CustomTask((g,c,s,t,stack) => {
                if (!c.HandZone.IsEmpty) Generic.DiscardBlock(c, c.HandZone[g.Random.Next(c.HandZone.Count)]);
                int slots = c.BoardZone.FreeSpace;
                for (int i=0;i<slots && !c.BoardZone.IsFull;i++) new SummonTask("UNG_829t3").Process(g,c,s,t,stack);
            }), ownerEnd: true, everyTick: true);
    }
}
