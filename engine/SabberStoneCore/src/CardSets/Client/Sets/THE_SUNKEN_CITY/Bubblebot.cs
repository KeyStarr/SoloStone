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
    internal static class Bubblebot
    {
        [StandardCard("TSC_059", 73428)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                foreach (var mech in c.BoardZone.GetAll(m => m.Id != s.Id && m.IsRace(Race.MECHANICAL))) {
                    new Effect(GameTag.DIVINE_SHIELD, EffectOperator.SET, 1).ApplyTo(mech);
                    Effects.TauntEff.ApplyTo(mech);
                }
            }) };
        }
    }
}
