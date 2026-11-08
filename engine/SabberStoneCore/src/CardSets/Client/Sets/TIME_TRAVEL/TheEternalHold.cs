using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TheEternalHold
    {
        [StandardCard("TIME_446",120999)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { LocationActivation=new LocationActivation { Task=new CustomTask(Resolve) } };
        }
        private static void Resolve(Game g,Controller c,IEntity s,IPlayable target,TaskStack stack)
        {
            if(target==null) {
                TimeTravelPastPools.Open(g,c,s,TimeTravelPastPools.EternalHoldOffers(g,c,s),new CustomTask(Resolve));
                s[GameTag.LOCATION_ACTION_COOLDOWN]=1;
                return;
            }
            if(c.DeckZone.Any(e=>e is Minion)) return;
            var actor=Enchantment.GetInstance(c,(IPlayable)s,c,Cards.FromId("TIME_446e"));
            CostRules.AddNextCardRule(c,cardType:CardType.MINION,setCost:1,enchantmentId:actor.Id);
        }
        [StandardCard("TIME_446e",121048)]
        internal static void ConfigureRule(Card card) { card.Implemented=true; card.ExcludeFromLegacyRandomPools=true; card.Power=new Power(); }
    }
}
