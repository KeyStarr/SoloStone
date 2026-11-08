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
    internal static class HamuulRunetotem
    {
        [StandardCard("EDR_845", 114858)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power();
            StartOfGameRules.Declare(card,s=>{
                var c=s.Controller;
                if(c.AppliedEnchantments?.Any(e=>e.Card.Id=="EDR_845e1")==true) return;
                ImbueRules.Imbue(c,s);
                var effect=Enchantment.GetInstance(c,s,c,Cards.FromId("EDR_845e1"),3);
                effect[GameTag.TAG_SCRIPT_DATA_NUM_1]=3;
                effect.Power.Trigger.Activate(effect);
            }, c=>c.DeckZone.Where(e=>e.Card.Type==CardType.SPELL).All(e=>e.Card[GameTag.SPELL_SCHOOL]==4));
        }
        [StandardCard("EDR_845e1", 117403)]
        internal static void Favor(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Trigger=new Trigger(TriggerType.AFTER_CAST) {
                TriggerSource=TriggerSource.FRIENDLY,
                SingleTask=new CustomTask((g,c,s,t,stack)=>{
                    if(--s[GameTag.TAG_SCRIPT_DATA_NUM_1]>0) return;
                    s[GameTag.TAG_SCRIPT_DATA_NUM_1]=3;
                    ImbueRules.Imbue(c,s);
                }) } };
        }
    }
}
