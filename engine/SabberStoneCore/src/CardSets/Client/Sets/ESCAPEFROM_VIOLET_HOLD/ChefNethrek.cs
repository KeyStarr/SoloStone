using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ChefNethrek
    {
        [StandardCard("JAIL_860",126045)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;card.Power=new Power();
            StartOfGameRules.Declare(card,s=>{
                var owner=s.Controller;
                if(owner.AppliedEnchantments?.Any(e=>e.Card.Id=="JAIL_860e")==true) return;
                var actor=Enchantment.GetInstance(owner,s,owner,Cards.FromId("JAIL_860e"));
                TurnQueueRules.Arm(actor,owner,5,atEnd:true);
                actor.Power.Trigger.Activate(actor);
            },owner=>owner.DeckZone.All(c=>c.Card.Cost<=3));
        }
        [StandardCard("JAIL_860e",126046)]
        internal static void Timer(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Trigger=new Trigger(TriggerType.TURN_END) {
                SingleTask=new CustomTask((g,c,s,t,stack)=>{
                    if(!TurnQueueRules.Tick(s,atEnd:true)) return;
                    int oldMaximum=c.BaseMana;c.BaseMana=10;g.Facts.BaseManaChanged(c,oldMaximum,s);c.UsedMana=0;c.TemporaryMana=0;c.OverloadLocked=0;
                    ((Enchantment)s).Remove();
                }) } };
        }
    }
}
