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
    internal static class Nythendra
    {
        [StandardCard("EDR_818",114307)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Tags[DeathContextRules.CaptureDeath] = 1;
            card.Power = new Power { DeathrattleTask = new CustomTask((g,c,s,t,stack) => {
                int count = Math.Min(c.BoardZone.FreeSpace, Math.Max(0,DeathContextRules.AttackAtDeath((IPlayable)s)));
                int position = Math.Min(((Minion)s).LastBoardPosition,c.BoardZone.SlotCount);
                for (int i=0;i<count && !c.BoardZone.IsFull;i++)
                    Generic.SummonBlock(g,(Minion)Entity.FromCard(c,Cards.FromId("EDR_818t")),position++,s);
            }) };
        }
        [StandardCard("EDR_818t",114313)]
        internal static void ConfigureBeetle(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                SingleTask = new CustomTask((g,c,s,t,stack) => {
                    if (!(s is Minion source) || source.Zone != c.BoardZone || source.IsSilenced || g.CurrentPlayer != c) return;
                    var beetles = c.BoardZone.Where(m=>m.Card.Id=="EDR_818t").ToArray();
                    int position = beetles.Min(m=>m.ZonePosition);
                    foreach (var beetle in beetles) { c.BoardZone.Remove(beetle); c.SetasideZone.Add(beetle); }
                    var definition = Cards.FromId("EDR_818").Clone();
                    definition.SetPrintedStats(beetles.Length, beetles.Length);
                    var result=(Minion)Entity.FromCard(c,definition);
                    Generic.SummonBlock(g,result,position,s);
                })
            } };
        }
    }
}
