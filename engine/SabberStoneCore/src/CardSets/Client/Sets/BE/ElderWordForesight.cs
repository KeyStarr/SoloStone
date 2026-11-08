using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ElderWordForesight
    {
        internal const int DbfId = 130401;
        internal static void Configure(Card card)
        {
            card.Implemented=true;
            card.ExcludeFromLegacyRandomPools=true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,k) => {
                Payload.Process(g,c,s,t,k);
                RepeatRules.GrantPayload(c,(IPlayable)s,"BE_221e");
            }) };
        }
        internal static readonly ISimpleTask Payload = new CustomTask((g,c,s,t,k) => {
            var drawn=new EffectContext(g,c.Id,s.Id).DrawEntity();
            if(drawn!=null && drawn.Zone==c.HandZone)
                Generic.AddEnchantmentBlock(g,Cards.FromId("BE_221e2"),(IPlayable)s,drawn);
        });
        internal static void ConfigurePayload(Card card) { card.Implemented=true; card.Power=new Power { PowerTask=Payload }; }
        internal static void ConfigureDiscount(Card card)
        {
            card.Implemented=true;
            card.Power=new Power { Enchant=new Enchant(Effects.ReduceCost(2)) };
        }
    }
}
