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
    internal static class Ghuun
    {
        [StandardCard("BE_287",130341)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;card.ConsumesHistory=true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1,Budget);
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>{
                int budget=Budget((IPlayable)s);
                for(int i=0;i<budget;i++)
                {
                    var enemies=c.Opponent.BoardZone.Where(m=>!m.Untouchable).Cast<ICharacter>()
                        .Append(c.Opponent.Hero).Where(e=>!e.IsDead).ToArray();
                    if(enemies.Length==0) break;
                    int lowest=enemies.Min(e=>e.Health);
                    var tied=enemies.Where(e=>e.Health==lowest).ToArray();
                    var victim=tied[g.Random.Next(tied.Length)];
                    Generic.AddEnchantmentBlock(g,Cards.FromId("BE_287e2"),(IPlayable)s,victim);
                    if(victim.Health<=0) victim.ToBeDestroyed=true;
                    Generic.AddEnchantmentBlock(g,Cards.FromId("BE_287e"),(IPlayable)s,s);
                }
            }) };
        }
        private static int Budget(IPlayable source)=>6+source.Game.HistoryFacts.Query.CompletedPlays()
            .Where(f=>f.ControllerId==source.Controller.Id && f.DefinitionKnown).Sum(f=>f.Definition.Materialize()[GameTag.COST_BLOOD]);
        [StandardCard("BE_287e",130637)]
        internal static void Boon(Card card)
        { card.Implemented=true;card.Power=new Power { Enchant=new Enchant(Effects.Health_N(1)) }; }
        [StandardCard("BE_287e2",131129)]
        internal static void Curse(Card card)
        { card.Implemented=true;card.Power=new Power { Enchant=new Enchant(Effects.Health_N(-1)) }; }
    }
}
