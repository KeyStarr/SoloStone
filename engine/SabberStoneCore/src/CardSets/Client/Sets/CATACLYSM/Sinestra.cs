using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Sinestra
    {
        internal const int DbfId = 125450;
        internal static void Configure(Card card)
        {
            card.Implemented=true;
            card.ExcludeFromLegacyRandomPools=true;
            ColossalRules.Declare(card,"CATA_154t","CATA_154t1");
            RepeatRules.Declare(card,RepeatRules.OtherClass);
            card.Power = new Power();
        }
        internal static void ConfigureWing(Card card)
        {
            card.Implemented=true;
            card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { Trigger=new Trigger(TriggerType.AFTER_SUMMON) { TriggerSource=TriggerSource.SELF,
                SingleTask=new CustomTask((g,c,s,t,k) => {
                    var chosen=new EffectContext(g,c.Id,s.Id).PickRandomCard(CataclysmPools.HeraldOtherClassSpell(),recipeDbfId:s.Card.AssetId);
                    if(chosen==null) return;
                    var generated=(Playable)Entity.FromCard(c,chosen,new EntityData { [GameTag.DISPLAYED_CREATOR]=s.Id });
                    CostRules.AddModifier(generated,EffectOperator.ADD,-((IPlayable)s)[GameTag.TAG_SCRIPT_DATA_NUM_1]);
                    Generic.AddHandPhase(c,generated);
                }) } };
        }
    }
}
