using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MerchSeller
    {
        internal const int DbfId = 130822;
        internal static OfflinePoolFilter Pool() => OfflinePoolFilter.Random(type:CardType.SPELL);
        [StandardCard("CORE_ETC_111", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Trigger=new Trigger(TriggerType.TURN_END) {
                SingleTask=new CustomTask((g,c,s,t,stack)=>{
                    if(c.Opponent.DeckZone.IsFull) return;
                    var pick=OfflineCardPools.Pick(g,Pool(),c.HeroClass,DbfId, PoolDestination.Deck);
                    if(pick!=null) c.Opponent.DeckZone.AddGenerated(pick,s);
                }) } };
        }
    }
}

namespace SabberStoneCore.CardSets.Client
{
    internal static class MerchSellerPools
    {
        [OfflinePool("CORE_ETC_111", Sets.MerchSeller.DbfId)]
        internal static PoolRequirement[] Declare() => new[] {
            new PoolRequirement("merch-seller-spell", Sets.MerchSeller.Pool(), new[] { "NEUTRAL" }, destination: PoolDestination.Deck) };
    }
}
