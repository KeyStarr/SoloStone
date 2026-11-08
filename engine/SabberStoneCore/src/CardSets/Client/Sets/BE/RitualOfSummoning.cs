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
    internal static class RitualOfSummoning
    {
        [StandardCard("BE_085", 129793)]
        internal static void Configure(Card card) => ObjectiveRules.Declare(card, 2,
            new CustomTask((g,c,s,t,stack) => {
                for (int i=0;i<2;i++) new SummonTask("BE_085t").Process(g,c,s,t,stack);
            }));
        [StandardCard("BE_085t", 129795)]
        internal static void Token(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; }
    }
}
