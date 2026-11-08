using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RaithVanGeist
    {
        [StandardCard("CAP_806",127068)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;card.ConsumesHistory=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>{
                var entries=g.HistoryFacts.Query.Reborns().Where(f=>f.ControllerId==c.Id).ToArray();
                var raised=new List<Minion>();
                foreach(var entry in entries)
                {
                    if(c.BoardZone.IsFull) break;
                    var body=(Minion)Entity.FromCard(c,entry.Definition.Materialize());
                    Generic.SummonBlock(g,body,-1,s);raised.Add(body);
                }
                foreach(var body in raised)
                {
                    if(body.Zone!=c.BoardZone || body.IsDead || body.Untouchable) continue;
                    var enemies=c.Opponent.BoardZone.Where(m=>!m.IsDead && !m.Untouchable).ToArray();
                    if(enemies.Length==0) break;
                    var target=enemies[g.Random.Next(enemies.Length)];
                    new AttackTask(EntityType.SOURCE,EntityType.TARGET).Process(g,c,body,target);
                }
            }) };
        }
    }
}
