using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client
{
    internal static class TimeTravelPastPools
    {
        [OfflinePool("END_027",121190)]
        internal static PoolRequirement[] END_027() => new[] {
                new PoolRequirement("dragon",OfflinePoolFilter.Discover(type:CardType.MINION,race:Race.DRAGON,scope:PoolScope.Past),new[]{"PRIEST"})
        };

        [OfflinePool("TIME_013",119686)]
        internal static PoolRequirement[] TIME_013() => new[] {
                new PoolRequirement("nature",OfflinePoolFilter.Discover(type:CardType.SPELL,spellSchool:4,scope:PoolScope.Past),new[]{"SHAMAN"}),
                new PoolRequirement("fallback",OfflinePoolFilter.Discover(type:CardType.SPELL,spellSchool:4,scope:PoolScope.Past),new[]{"SHAMAN"})
        };

        [OfflinePool("TIME_016",119964)]
        internal static PoolRequirement[] TIME_016() => new[] {
                new PoolRequirement("mech",new OfflinePoolFilter(PoolScope.Past,PoolClassMode.Exact,CardClass.PALADIN,CardType.MINION,race:Race.MECHANICAL),new[]{"PALADIN"})
        };

        [OfflinePool("TIME_444",120993)]
        internal static PoolRequirement[] TIME_444() => new[] {
                new PoolRequirement("demon",OfflinePoolFilter.Random(type:CardType.MINION,race:Race.DEMON,scope:PoolScope.Past),new[]{"DEMONHUNTER"})
        };

        [OfflinePool("TIME_446",120999)]
        internal static PoolRequirement[] TIME_446() => new[] {
                new PoolRequirement("demon",OfflinePoolFilter.Random(type:CardType.MINION,race:Race.DEMON,minCost:5),new[]{"DEMONHUNTER"}),
                new PoolRequirement("wild-demon",OfflinePoolFilter.Random(type:CardType.MINION,race:Race.DEMON,minCost:5,scope:PoolScope.Past),new[]{"DEMONHUNTER"})
        };

        [OfflinePool("TIME_704",120761)]
        internal static PoolRequirement[] TIME_704() => new[] {
                new PoolRequirement("spell",OfflinePoolFilter.Discover(type:CardType.SPELL,minCost:7,scope:PoolScope.Past),new[]{"DRUID"})
        };

        [OfflinePool("TIME_707",122833)]
        internal static PoolRequirement[] TIME_707() => new[] {
                new PoolRequirement("choose-one",OfflinePoolFilter.Random(keyword:GameTag.CHOOSE_ONE,scope:PoolScope.Past),new[]{"DRUID"}),
                new PoolRequirement("choose-one-deck",OfflinePoolFilter.Random(keyword:GameTag.CHOOSE_ONE,scope:PoolScope.Past),new[]{"DRUID"}, destination: PoolDestination.Deck)
        };

        [OfflinePool("TIME_711",120793)]
        internal static PoolRequirement[] TIME_711() => new[] {
                new PoolRequirement("one",OfflinePoolFilter.Random(type:CardType.MINION,minCost:1,maxCost:1,scope:PoolScope.Past),new[]{"ROGUE"}, destination: PoolDestination.Summon)
        };

        [OfflinePool("TIME_857",119633)]
        internal static PoolRequirement[] TIME_857() => new[] {
                new PoolRequirement("arcane",OfflinePoolFilter.Discover(type:CardType.SPELL,spellSchool:1,scope:PoolScope.Past),new[]{"MAGE"})
        };

        private static PoolRequirement Requirement(string id,string name) => OfflinePoolDeclarations.Cards.Single(d=>d.CardId==id).Pools.Single(p=>p.Name==name);
        internal static OfflinePoolFilter Filter(string id,string name) => Requirement(id,name).Filter;
        internal static Card[] Offers(Game g,Controller c,IEntity source,string id,string name,CardClass? hero=null) =>
            OfflineCardPools.SampleDistinct(g,Filter(id,name),hero??c.HeroClass,source.Card.AssetId,Requirement(id,name).Destination,OfflineCardPools.PrintedClassMask(source.Card));
        internal static Card Pick(Game g,Controller c,IEntity source,string id,string name) =>
            OfflineCardPools.Pick(g,Filter(id,name),c.HeroClass,source.Card.AssetId,Requirement(id,name).Destination);
        internal static Card[] EternalHoldOffers(Game g,Controller c,IEntity source)
        {
            if(g.FormatType!=FormatType.FT_WILD) return Offers(g,c,source,"TIME_446","demon");
            var candidates=OfflineCardPools.Candidates(g,c.HeroClass,Filter("TIME_446","demon"),source.Card.AssetId,sourceClassMask:OfflineCardPools.PrintedClassMask(source.Card))
                .Concat(OfflineCardPools.Candidates(g,c.HeroClass,Filter("TIME_446","wild-demon"),source.Card.AssetId,sourceClassMask:OfflineCardPools.PrintedClassMask(source.Card)))
                .OrderBy(card=>card.AssetId).ToArray();
            return new EffectContext(g,c.Id,source.Id).SampleDistinct(candidates,Math.Min(3,candidates.Length));
        }
        internal static void Open(Game g,Controller c,IEntity source,Card[] offers,ISimpleTask afterChoose=null)
        {
            if(!new EffectContext(g,c.Id,source.Id).Discover(offers,afterChoose:afterChoose)) return;
            var choice=c.Choice;
            while(choice.NextChoice!=null) choice=choice.NextChoice;
            foreach(int entity in choice.Choices) g.IdEntityDic[entity][Generic.PrivateDecisionOfferTag]=c.PlayerId;
        }
    }
}
