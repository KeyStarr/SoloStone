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
    internal static class KaldoreiCultivator
    {
        internal const int DbfId=121460;
        internal static OfflinePoolFilter Pool() => OfflinePoolFilter.Discover(type:CardType.MINION,race:Race.BEAST);
        [StandardCard("TIME_730", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new DecisionBattlecryTask("client/TIME_730/v1",
                new DiscoverThenContinueTask(Pool(),recipeDbfId:DbfId),
                new CustomTask((g,c,s,t,stack)=>{ stack.Number=stack.Playables?.LastOrDefault()?.Id??0; stack.Playables?.Clear(); }),
                new DiscoverThenContinueTask(Pool(),recipeDbfId:DbfId),
                new CustomTask((g,c,s,t,stack)=>{
                    var selected=new[]{stack.Number,stack.Playables?.LastOrDefault()?.Id??0}.Where(id=>id!=0).Select(id=>g.IdEntityDic[id]).ToArray();
                    if(selected.Length==2 && g.Random.Next(2)==1) Array.Reverse(selected);
                    foreach(var e in selected) Generic.AddEnchantmentBlock(g,Cards.FromId("TIME_730e"),(IPlayable)s,e);
                    foreach(var e in selected) {
                        if(c.DeckZone.IsFull) break;
                        c.SetasideZone.Remove(e); e[GameTag.ZONE]=(int)Zone.DECK; c.DeckZone.Add(e,0);
                    }
                })) };
        }
        [StandardCard("TIME_730e", 122835)]
        internal static void Buff(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Enchant=new Enchant(Effects.AttackHealth_N(5)) };
        }
    }
}

namespace SabberStoneCore.CardSets.Client
{
    internal static class KaldoreiCultivatorPools
    {
        [OfflinePool("TIME_730", Sets.KaldoreiCultivator.DbfId)]
        internal static PoolRequirement[] Declare() => new[] {
            new PoolRequirement("cultivator-beast", Sets.KaldoreiCultivator.Pool(), new[] { "DRUID" }, destination: PoolDestination.Deck) };
    }
}
