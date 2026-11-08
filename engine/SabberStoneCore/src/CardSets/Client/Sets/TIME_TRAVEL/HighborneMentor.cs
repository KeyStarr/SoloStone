using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HighborneMentor
    {
        [StandardCard("TIME_704",120761)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new DecisionBattlecryTask("client/TIME_704/v1",
                new CustomTask((g,c,s,t,stack)=> {
                    var pupil=Entity.FromCard(c,Cards.FromId("TIME_704t"),new EntityData { [GameTag.CREATOR]=s.Id,[GameTag.DISPLAYED_CREATOR]=s.Id });
                    stack.Number=pupil.Id; Generic.AddHandPhase(c,pupil);
                }),
                new DiscoverThenContinueTask((g,c,s,stack)=>TimeTravelPastPools.Offers(g,c,s,"TIME_704","spell",CardClass.DRUID), DiscoverRealRules.PrepareDiscover),
                new CustomTask((g,c,s,t,stack)=> {
                    if(!g.IdEntityDic.TryGetValue(stack.Number,out var pupil) || pupil.Zone!=c.HandZone) return;
                    var chosen=stack.Playables.FirstOrDefault(); if(chosen==null) return;
                    var taught=PayloadRules.StoreCard(pupil,1,chosen.Card,PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.ShowOwner);
                    pupil[GameTag.TAG_SCRIPT_DATA_NUM_1]=taught.Id;
                })) };
        }
        [StandardCard("TIME_704t",122590)]
        internal static void ConfigurePupil(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1,PayloadRules.FirstReference);
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=> {
                var taught=PayloadRules.Entries(s,1).FirstOrDefault(); if(taught==null) return;
                var spell=Entity.FromCard(c,taught.Card,new EntityData { [GameTag.DISPLAYED_CREATOR]=s.Id });
                var cast=new TaskStack(); cast.AddPlayable(spell);
                new PlayTask(PlayType.SPELL,true).Process(g,c,s,null,cast);
            }) };
        }
    }
}
