using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Teamwork
    {
        [StandardCard("MEND_900", 127427)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                for (int i=0;i<4;i++)
                {
                    if (!c.BoardZone.IsFull)
                        Generic.SummonBlock(g, (Minion)Entity.FromCard(c, Cards.FromId("CS2_101t")), -1, s);
                    g.DeathProcessingAndAuraUpdate();
                }
                for (int i=0;i<4 && !c.HandZone.IsFull;i++)
                    Generic.AddHandPhase(c, Entity.FromCard(c, Cards.FromId("CS2_101t"),
                        new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }));
            }) };
        }
    }
}
