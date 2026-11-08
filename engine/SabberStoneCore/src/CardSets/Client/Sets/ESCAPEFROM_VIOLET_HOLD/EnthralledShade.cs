using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EnthralledShade
    {
        [StandardCard("JAIL_434",126127)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g,c,s,t,stack) => {
                foreach (var held in c.HandZone.Where(PayloadRules.CopiedFromOpponent).ToArray())
                    CostRules.AddModifier((Playable)held, EffectOperator.SUB, 1);
            }) };
        }
    }
}
