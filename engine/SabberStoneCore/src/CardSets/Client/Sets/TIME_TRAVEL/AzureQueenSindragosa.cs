using System;
using System.Linq;
using System.Collections.Generic;
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
    internal static class AzureQueenSindragosa
    {
        [StandardCard("TIME_852",119524)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Aura=new Aura(AuraType.HAND,Effects.ReduceCost(2)) {
                Restless=true, Condition=new SelfCondition(p=>p is Spell && p.Card[GameTag.SPELL_SCHOOL]==1
                    && p.Controller.BoardZone.Count(m=>m.Card.IsRace(Race.DRAGON) && !m.IsDormant && !m.ToBeDestroyed)>1)
            } };
        }
        [StandardCard("TIME_852t1",119527)]
        internal static void Malygos(Card card) { card.Implemented=true; card.ExcludeFromLegacyRandomPools=true; card.Power=new Power(); }
        [StandardCard("TIME_852t3",119525)]
        internal static void Oathstone(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true; card.ConsumesHistory=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,k)=> {
                var deaths=g.HistoryFacts.Query.Deaths().Where(f=>f.ControllerId==c.Id && (f.Races.Contains(Race.DRAGON) || f.Races.Contains(Race.ALL))).ToArray();
                var ctx=new EffectContext(g,c.Id,s.Id);
                foreach(var death in deaths.ChooseNElements(deaths.Length,g.Random))
                { if(c.BoardZone.IsFull) break; ctx.Summon(death.Definition.ReadDefinition().Id); }
            }) };
        }
    }
}
