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
    internal static class RitualOfExcess
    {
        [StandardCard("BE_005", 132346)]
        internal static void Configure(Card card) => ObjectiveRules.Declare(card, 2, new CustomTask((g,c,s,t,stack) => {
            for (int i = 0; i < 2 && g.State != State.COMPLETE; i++)
            {
                var drawn = Generic.Draw(c);
                if (drawn?.Zone == c.HandZone)
                    Generic.AddEnchantmentBlock(g, Cards.FromId("BE_221e2"), (IPlayable)s, drawn);
                g.DeathProcessingAndAuraUpdate();
            }
        }));
    }
}
